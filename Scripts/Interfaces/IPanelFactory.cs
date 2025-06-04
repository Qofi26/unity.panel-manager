using Cysharp.Threading.Tasks;
using UnityEngine;

namespace QModules.PanelManager
{
    public interface IPanelFactory
    {
        public UniTask<T> InstantiateAsync<T>(string key, Transform parent) where T : IPanel;
        public void ReleaseInstance(IPanel? panel);
        public void ReleaseAll();
    }
}
