using ShiftCal.Core;
using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class DayPreview : MonoBehaviour
    {
        public Text title, shift, details;
        public Image swatch;
        public RectTransform card, scroll;
        public DayDetailsPopup editor;
        public string DateKey => selected?.dateKey;
        private CalendarDayData selected;
        private DayInformation information;
        private bool sizePending;
        private Vector2 previousSize;
        private void OnRectTransformDimensionsChange() => sizePending = true;
        private void LateUpdate() => Fit();
        public void Show(CalendarDayData day, DayInformation info)
        {
            selected = day; information = info; title.text = day.date.ToString("dddd, MMMM d");
            shift.text = day.shiftName; swatch.color = Core.ShiftStyleUtility.ToColor(day.shiftColorHex); details.text = info.Describe(day);
            gameObject.SetActive(true); transform.SetAsLastSibling();
            sizePending = true;
        }
        public void Fit()
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics()) return;
            var size = ((RectTransform)transform).rect.size;
            if (!sizePending && size == previousSize) return;
            sizePending = false; previousSize = size;
            float width = Mathf.Max(100, scroll.rect.width - 56);
            float body = details.cachedTextGeneratorForLayout.GetPreferredHeight(details.text, details.GetGenerationSettings(new Vector2(width, 0))) / details.pixelsPerUnit;
            float height = Mathf.Clamp(body + 340, 470, ((RectTransform)transform).rect.height * .64f);
            card.pivot = new Vector2(.5f, 0);
            card.sizeDelta = new Vector2(card.sizeDelta.x, height);
            card.anchoredPosition = new Vector2(0, 24);
        }
        public void OpenDetails() { Hide(); editor.Show(selected, information); }
        public void Hide() => gameObject.SetActive(false);
    }
}
