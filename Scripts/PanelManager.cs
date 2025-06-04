using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace QModules.PanelManager
{
    public class PanelManager : IPanelManager
    {
        public event Action<IPanel>? OnPanelShown;
        public event Action<IPanel>? OnPanelClosed;

        public Canvas RootCanvas { get; private set; } = null!;

        private readonly IPanelFactory _panelFactory;
        private readonly Action<IPanel> _onPanelCreated;
        private readonly Dictionary<Type, IPanel> _activePanels;
        private readonly Dictionary<Type, CancellationTokenSource> _panelsTasks;

        private Canvas _nestedCanvas = null!;

        public PanelManager(IPanelFactory panelFactory, Action<IPanel>? onPanelCreated)
        {
            _panelFactory = panelFactory;
            _onPanelCreated = onPanelCreated ?? (_ => { });
            _activePanels = new Dictionary<Type, IPanel>();
            _panelsTasks = new Dictionary<Type, CancellationTokenSource>();
        }

        public void Initialize(Canvas rootCanvas)
        {
            RootCanvas = rootCanvas;
            _nestedCanvas = PanelManagerUtils.CreateNestedCanvas(RootCanvas);
        }

        public void Deinitialize()
        {
            var panels = _activePanels.Values.ToList();

            foreach (var panel in panels)
            {
                Close(panel);
            }

            _activePanels.Clear();
            _panelsTasks.Clear();

            _panelFactory.ReleaseAll();

            RootCanvas = null!;
        }

        #region IPanelManager

        public async UniTask<T> Show<T>(Transform? parent = null, int? sorting = null) where T : IPanel
        {
            var view = await InstantiatePanel<T>(parent, sorting);

            if (view == null)
            {
                Debug.LogError($"[{GetType().Name}] [{nameof(Show)}] Showing view of type {typeof(T).Name} cancelled");
                return default!;
            }

            view.Activate();
            OnPanelShown?.Invoke(view);
            return view;
        }

        public async UniTask<T> Show<T, TArgs>(TArgs args, Transform? parent = null, int? sorting = null)
            where T : IPanel<TArgs>
        {
            var view = await InstantiatePanel<T>(parent, sorting);

            if (view == null)
            {
                Debug.LogError($"[{GetType().Name}] [{nameof(Show)}] Showing view of type {typeof(T).Name} cancelled");
                return default!;
            }

            view.Activate(args);
            OnPanelShown?.Invoke(view);
            return view;
        }

        public bool TryGet<T>(out T view) where T : IPanel
        {
            if (!Has<T>())
            {
                view = default!;
                return false;
            }

            view = Get<T>()!;

            return true;
        }

        public T? Get<T>() where T : IPanel
        {
            if (_activePanels.TryGetValue(typeof(T), out var view))
            {
                return (T) view;
            }

            return default;
        }

        public bool Has<T>(bool includeWait = false) where T : IPanel
        {
            var type = typeof(T);
            return Has(type, includeWait);
        }

        public bool Has(Type type, bool includeWait = false)
        {
            if (includeWait && _panelsTasks.ContainsKey(type))
            {
                return true;
            }

            return _activePanels.ContainsKey(type);
        }

        public bool Has(IPanel? view)
        {
            return view != null && _activePanels.ContainsKey(view.GetType());
        }

        public bool TryClose<T>() where T : IPanel
        {
            if (!Has<T>())
            {
                return false;
            }

            Close<T>();
            return true;
        }

        public bool TryClose(IPanel? view)
        {
            if (!Has(view))
            {
                return false;
            }

            Close(view);
            return true;
        }

        public void Close<T>() where T : IPanel
        {
            var view = Get<T>();
            if (view == null)
            {
                Debug.LogError($"[{nameof(PanelManager)}] [{nameof(Close)}] View doesn't exist: {typeof(T).Name}");
                return;
            }

            Close(view);
        }

        public void Close(IPanel? view)
        {
            if (view == null)
            {
                Debug.LogError($"[{nameof(PanelManager)}] [{nameof(Close)}] View doesn't exist");
                return;
            }

            if (!_activePanels.Remove(view.GetType()))
            {
                return;
            }

            Debug.Log($"[{nameof(PanelManager)}] [{nameof(Close)}] {view.name}");

            view.Deactivate();
            view.Dispose();

            _panelFactory.ReleaseInstance(view);

            OnPanelClosed?.Invoke(view);
        }

        public bool TryCancelViewShowingProcess<T>() where T : IPanel
        {
            if (!_panelsTasks.TryGetValue(typeof(T), out var cancellationTokenSource))
            {
                return false;
            }

            cancellationTokenSource.Cancel();
            return true;
        }

        public void OverrideSortingOrder<T>(int sortingOrder, int offset = 0) where T : IPanel
        {
            if (!TryGet(out T view))
            {
                return;
            }

            if (!view.gameObject.TryGetComponent<Canvas>(out var canvas))
            {
                return;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder + offset;
        }

        public void ResetSortingOrder<T>() where T : IPanel
        {
            if (TryGet(out T view))
            {
                if (view.gameObject.TryGetComponent<Canvas>(out var canvas))
                {
                    if (PanelManagerUtils.TryGetSortingOrder<T>(out var sortingOrder))
                    {
                        canvas.overrideSorting = true;
                        canvas.sortingOrder = sortingOrder;
                    }
                }
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
                Debug.Log($"[{nameof(PanelManager)}] Show view already in progress: {type.Name}");

                await UniTask.WaitWhile(() => _panelsTasks.ContainsKey(type) && !Has<T>());

                if (TryGet<T>(out var result))
                {
                    return result;
                }
            }
            else
            {
                Debug.Log($"[{nameof(PanelManager)}] Show: {type.Name}");
            }

            var cancellation = new CancellationTokenSource();

            if (!parent)
            {
                parent = RootCanvas.transform;
            }

            var task = InstantiatePanelInternal<T>(cancellation.Token, parent, sorting);

            _panelsTasks.Add(type, cancellation);

            var view = await task;

            _panelsTasks.Remove(type);

            return view;
        }

        private async UniTask<T> InstantiatePanelInternal<T>(
            CancellationToken cancellation,
            Transform? parent,
            int? sorting = null) where T : IPanel
        {
            var type = typeof(T);

            var owner = parent == null
                ? RootCanvas.transform
                : parent;

            var panelName = type.Name;

            var panelTask = _panelFactory.InstantiateAsync<T>(type.Name, owner);

            var panel = await panelTask;

            if (cancellation.IsCancellationRequested)
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
    }
}
