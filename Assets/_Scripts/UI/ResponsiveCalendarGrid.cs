using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    [ExecuteAlways, RequireComponent(typeof(GridLayoutGroup))]
    public class ResponsiveCalendarGrid : MonoBehaviour
    {
        private bool pending = true;
        private GridLayoutGroup grid;
        private Rect previous;
        private Vector2 previousSpacing;
        private int previousHorizontal, previousVertical;
        private void OnEnable() => pending = true;
        // Layout callbacks only invalidate our cache. Never mutate the canvas here.
        private void OnRectTransformDimensionsChange() => pending = true;
        private void LateUpdate() => Fit();
        public void Fit()
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics()) { pending = true; return; }
            if (grid == null) grid = GetComponent<GridLayoutGroup>();
            var rect = ((RectTransform)transform).rect;
            if (!pending && rect == previous && grid.spacing == previousSpacing && grid.padding.horizontal == previousHorizontal && grid.padding.vertical == previousVertical) return;
            pending = false; previous = rect; previousSpacing = grid.spacing;
            previousHorizontal = grid.padding.horizontal; previousVertical = grid.padding.vertical;
            var size = new Vector2(Mathf.Max(1, (rect.width - grid.spacing.x * 6 - previousHorizontal) / 7), Mathf.Max(1, (rect.height - grid.spacing.y * 5 - previousVertical) / 6));
            if ((grid.cellSize - size).sqrMagnitude > .01f) grid.cellSize = size;
        }
    }
}
