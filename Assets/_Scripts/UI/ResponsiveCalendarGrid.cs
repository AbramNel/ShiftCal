using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    [ExecuteAlways,RequireComponent(typeof(GridLayoutGroup))]
    public class ResponsiveCalendarGrid : MonoBehaviour
    {
        private void OnRectTransformDimensionsChange()
        { Fit(); }
        private void LateUpdate() { Fit(); }
        public void Fit()
        {
            var grid=GetComponent<GridLayoutGroup>();if(grid==null)return;
            var rect=((RectTransform)transform).rect;
            grid.cellSize=new Vector2(Mathf.Max(1,(rect.width-grid.spacing.x*6-grid.padding.horizontal)/7),Mathf.Max(1,(rect.height-grid.spacing.y*5-grid.padding.vertical)/6));
        }
    }
}
