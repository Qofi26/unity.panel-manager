using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace PanelManager
{
    internal static class PanelManagerUtils
    {
        public static bool TryGetSortingOrder<T>(out int sortingOrder, int? overrideSorting = null)
        {
            return TryGetSortingOrder(typeof(T), out sortingOrder, overrideSorting);
        }

        public static bool TryGetSortingOrder(MemberInfo memberInfo, out int sortingOrder, int? overrideSorting = null)
        {
            if (overrideSorting.HasValue)
            {
                sortingOrder = overrideSorting.Value;
                return true;
            }

            var attribute = memberInfo.GetCustomAttribute<PanelSortingAttribute>();
            if (attribute == null)
            {
                sortingOrder = 0;
                return false;
            }

            sortingOrder = attribute.SortingOrder;
            return true;
        }

        public static Canvas CreateNestedCanvas(Canvas rootCanvas, string name = "Panels")
        {
            var nestedObject = new GameObject(name);
            nestedObject.transform.SetParent(rootCanvas.transform);
            var nestedCanvas = nestedObject.AddComponent<Canvas>();
            nestedObject.AddComponent<GraphicRaycaster>();

            CopyCanvasSettings(rootCanvas, nestedCanvas);

            var nestedTransform = nestedObject.GetComponent<RectTransform>();

            nestedTransform.anchorMin = Vector2.zero;
            nestedTransform.anchorMax = Vector2.one;
            nestedTransform.sizeDelta = Vector2.zero;
            nestedTransform.anchoredPosition = Vector2.zero;
            nestedTransform.pivot = new Vector2(0.5f, 0.5f);

            return nestedCanvas;
        }

        private static void CopyCanvasSettings(Canvas origin, Canvas other)
        {
            other.vertexColorAlwaysGammaSpace = origin.vertexColorAlwaysGammaSpace;
            other.additionalShaderChannels = origin.additionalShaderChannels;
            other.renderMode = origin.renderMode;
            other.worldCamera = origin.worldCamera;
        }
    }
}
