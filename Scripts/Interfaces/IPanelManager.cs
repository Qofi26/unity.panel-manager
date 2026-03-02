using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PanelManager
{
    public interface IPanelManager
    {
        public event Action<IPanel> OnPanelShow;
        public event Action<IPanel> OnPanelHide;

        public UniTask<T> Show<T>(Transform? parent = null, int? sorting = null) where T : IPanel;

        public UniTask<T> Show<T, TArgs>(TArgs args, Transform? parent = null, int? sorting = null)
            where T : IPanel<TArgs>;

        public UniTask<T?> Get<T>() where T : IPanel;
        public UniTask<IPanel?> Get(Type type);

        public bool Has<T>(bool includeWait = false) where T : IPanel;
        public bool Has(Type type, bool includeWait = false);

        public UniTask TryClose<T>() where T : IPanel;
        public UniTask TryClose(Type type);
        public UniTask TryClose(IPanel? panel);

        public UniTask OverrideSortingOrder<T>(int sortingOrder) where T : IPanel;
        public UniTask ResetSortingOrder<T>() where T : IPanel;
    }
}
