using System;

namespace QModules.PanelManager
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PanelSortingAttribute : Attribute
    {
        public int SortingOrder { get; }

        public PanelSortingAttribute(int sortingOrder)
        {
            SortingOrder = sortingOrder;
        }
    }
}
