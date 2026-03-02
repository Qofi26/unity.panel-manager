// ReSharper disable InconsistentNaming

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PanelManager
{
    public interface IPanel : IDisposable
    {
        public string name { get; }
        public GameObject gameObject { get; }

        public void Activate();
        public void Deactivate();

        public async UniTask Show()
        {
            Activate();
            await ActivateAnimate();
        }

        public async UniTask Hide()
        {
            await DeactivateAnimate();
            Deactivate();
        }

        protected UniTask ActivateAnimate();
        protected UniTask DeactivateAnimate();
    }

    public interface IPanel<in TArgs> : IPanel
    {
        public void Activate(TArgs args);

        public async UniTask Show(TArgs args)
        {
            Activate(args);
            await ActivateAnimate();
        }
    }
}
