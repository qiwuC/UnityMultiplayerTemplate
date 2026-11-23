using UnityEngine;

namespace Cqw.MultilayerFramework.UI
{
    /// <summary>Canvas 层级常量（sortingOrder 递增，后层覆盖前层）。</summary>
    public static class UICanvasLayers
    {
        public const int Background = 0;
        public const int Main = 10;
        public const int Panel = 20;
        public const int Popup = 30;
        public const int Toast = 40;
        public const int Loading = 50;
        public const int World = 60; // World Space Canvas 不参与 sortingOrder 排序，保留常量命名
    }
}
