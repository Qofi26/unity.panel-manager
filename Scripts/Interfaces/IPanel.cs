using System;
using UnityEngine;

namespace QModules.PanelManager
{
    public interface IPanel : IDisposable
    {
        // ReSharper disable once InconsistentNaming
        public string name { get; }

        // ReSharper disable once InconsistentNaming
        public GameObject gameObject { get; }

        public void Activate();
        public void Deactivate();

        // TODO: activate and deactivate with animate
    }

    public interface IPanel<in TArgs> : IPanel
    {
        public void Activate(TArgs args);
    }
}
