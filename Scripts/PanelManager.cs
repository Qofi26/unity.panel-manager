using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PanelManager
{
    public class PanelManager : IPanelManager, IDisposable
    {
        public event Action<IPanel> OnPanelShow = delegate { };
        public event Action<IPanel> OnPanelHide = delegate { };

        public Canvas RootCanvas { get; private set; } = null!;

        private readonly IPanelFactory _panelFactory;
        private readonly Action<IPanel> _onPanelCreated;

        private readonly Dictionary<Type, IPanel> _activePanels = new();
        private readonly Dictionary<Type, CancellationTokenSource> _panelsTasks = new();

        private bool _isInitialized;
        private Canvas _nestedCanvas = null!;

        public PanelManager(IPanelFactory panelFactory, Action<IPanel>? onPanelCreated)
        {
            _panelFactory = panelFactory;
            _onPanelCreated = onPanelCreated ?? delegate { };
        }

        public void Initialize(Canvas rootCanvas)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            RootCanvas = rootCanvas;
            _nestedCanvas = PanelManagerUtils.CreateNestedCanvas(RootCanvas);
        }

        public void Dispose()
        {
            if (!_isInitialized)
            {
                return;
            }

            _isInitialized = false;

            var panels = _activePanels.Values.ToList();

            foreach (var panel in panels)
            {
                Debug.Log($"[{GetType().Name}] Close {panel.name}");

                panel.Deactivate();
                panel.Dispose();
                _panelFactory.ReleaseInstance(panel);
            }

            foreach (var tokenSource in _panelsTasks.Values)
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }

            _activePanels.Clear();
            _panelsTasks.Clear();

            RootCanvas = null!;

            OnPanelShow = delegate { };
            OnPanelHide = delegate { };
        }

        #region IPanelManager

        public async UniTask<T> Show<T>(Transform? parent = null, int? sorting = null) where T : IPanel
        {
            var panel = await InstantiatePanel<T>(parent, sorting);

            if (panel == null)
            {
                Debug.LogError($"[{GetType().Name}] [Show] Showing view of type {typeof(T).Name} cancelled");
                return default!;
            }

            await panel.Show();

            OnPanelShow(panel);
            return panel;
        }

        public async UniTask<T> Show<T, TArgs>(TArgs args, Transform? parent = null, int? sorting = null)
            where T : IPanel<TArgs>
        {
            var panel = await InstantiatePanel<T>(parent, sorting);

            if (panel == null)
            {
                Debug.LogError($"[{GetType().Name}] [Show] Showing view of type {typeof(T).Name} cancelled");
                return default!;
            }

            await panel.Show(args);

            OnPanelShow(panel);
            return panel;
        }

        public async UniTask<T?> Get<T>() where T : IPanel
        {
            var type = typeof(T);

            await WaitShow(type);

            if (_activePanels.TryGetValue(type, out var view))
            {
                return (T) view;
            }

            return default;
        }

        public async UniTask<IPanel?> Get(Type type)
        {
            await WaitShow(type);

            return _activePanels.GetValueOrDefault(type);
        }

        public bool Has<T>(bool includeInProcessShow = false) where T : IPanel
        {
            var type = typeof(T);
            return Has(type, includeInProcessShow);
        }

        public bool Has(Type type, bool includeWait = false)
        {
            if (includeWait && _panelsTasks.ContainsKey(type))
            {
                return true;
            }

            return _activePanels.ContainsKey(type);
        }

        public UniTask TryClose<T>() where T : IPanel
        {
            var type = typeof(T);
            return TryClose(type);
        }

        public async UniTask TryClose(Type type)
        {
            if (TryCancelPanelTask(type))
            {
                return;
            }

            var panel = await Get(type);
            await TryClose(panel);
        }

        public async UniTask TryClose(IPanel? panel)
        {
            if (panel == null)
            {
                return;
            }

            var type = panel.GetType();

            _activePanels.Remove(type);
            Debug.Log($"[{GetType().Name}] [Close] {panel.name}");

            await panel.Hide();
            OnPanelHide(panel);
            panel.Dispose();
            _panelFactory.ReleaseInstance(panel);
        }

        public async UniTask OverrideSortingOrder<T>(int sortingOrder) where T : IPanel
        {
            var panel = await Get<T>();

            if (panel == null || !panel.gameObject.TryGetComponent<Canvas>(out var canvas))
            {
                return;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        public async UniTask ResetSortingOrder<T>() where T : IPanel
        {
            var panel = await Get<T>();

            if (panel == null || !panel.gameObject.TryGetComponent<Canvas>(out var canvas))
            {
                return;
            }

            if (PanelManagerUtils.TryGetSortingOrder<T>(out var sortingOrder))
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = sortingOrder;
            }
        }

        #endregion

        private async UniTask<T?> InstantiatePanel<T>(Transform? parent, int? sorting = null) where T : IPanel
        {
            var type = typeof(T);

            if (_activePanels.TryGetValue(type, out var activeView))
            {
                return (T) activeView;
            }

            if (_panelsTasks.ContainsKey(type))
            {
                Debug.Log($"[{GetType().Name}] Show view already in progress: {type.Name}");

                var result = await Get<T>();
                return result;
            }

            Debug.Log($"[{GetType().Name}] Show: {type.Name}");

            var cancellation = new CancellationTokenSource();
            _panelsTasks.Add(type, cancellation);

            if (!parent)
            {
                parent = RootCanvas.transform;
            }

            var panel = await InstantiatePanelInternal<T>(cancellation.Token, parent, sorting);

            _panelsTasks.Remove(type);

            return panel;
        }

        private async UniTask<T> InstantiatePanelInternal<T>(
            CancellationToken token,
            Transform? parent,
            int? sorting = null) where T : IPanel
        {
            var type = typeof(T);

            var owner = !parent
                ? RootCanvas.transform
                : parent;

            var panelName = type.Name;

            var panel = await _panelFactory.InstantiateAsync<T>(type.Name, owner);

            if (token.IsCancellationRequested)
            {
                _panelFactory.ReleaseInstance(panel);
                return default!;
            }

            if (panel.gameObject.TryGetComponent<Canvas>(out var canvas))
            {
                if (PanelManagerUtils.TryGetSortingOrder<T>(out var sortingOrder, sorting))
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = sortingOrder;
                }
            }
            else if (panel.gameObject.transform.parent == RootCanvas.transform)
            {
                panel.gameObject.transform.SetParent(_nestedCanvas.transform);
            }

            panel.gameObject.name = panelName;
            _activePanels[type] = panel;
            _onPanelCreated(panel);

            return panel;
        }

        private bool TryCancelPanelTask(Type type)
        {
            if (!_panelsTasks.TryGetValue(type, out var token))
            {
                return false;
            }

            token.Cancel();
            token.Dispose();
            _panelsTasks.Remove(type);
            return true;
        }

        private async UniTask<bool> WaitShow(Type type)
        {
            if (!_panelsTasks.ContainsKey(type))
            {
                return false;
            }

            await UniTask.WaitWhile(() => _panelsTasks.ContainsKey(type));

            return true;
        }
    }
}
