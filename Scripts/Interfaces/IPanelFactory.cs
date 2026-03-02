using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PanelManager
{
    public interface IPanelFactory
    {
        public UniTask<T> InstantiateAsync<T>(string key, Transform parent) where T : IPanel;
        public void ReleaseInstance(IPanel? panel);
    }
}
