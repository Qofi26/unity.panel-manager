using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace QModules.PanelManager
{
    public interface IPanelManager
    {
        public UniTask<T> Show<T>(Transform? parent = null, int? sorting = null) where T : IPanel;

        public UniTask<T> Show<T, TArgs>(TArgs args, Transform? parent = null, int? sorting = null)
            where T : IPanel<TArgs>;

        public bool TryGet<T>(out T view) where T : IPanel;
        public T? Get<T>() where T : IPanel;

        public bool Has<T>(bool includeWait = false) where T : IPanel;
        public bool Has(Type type, bool includeWait = false);
        public bool Has(IPanel? view);

        public bool TryClose<T>() where T : IPanel;
        public bool TryClose(IPanel? view);
        public void Close<T>() where T : IPanel;
        public void Close(IPanel? view);

        public bool TryCancelViewShowingProcess<T>() where T : IPanel;

        public void OverrideSortingOrder<T>(int sortingOrder, int offset = 0) where T : IPanel;
        public void ResetSortingOrder<T>() where T : IPanel;
    }
}
