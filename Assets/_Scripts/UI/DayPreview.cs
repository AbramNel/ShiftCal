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
        public void Show(CalendarDayData day, DayInformation info)
        {
            selected = day; information = info; title.text = day.date.ToString("dddd, MMMM d");
            shift.text = day.shiftName; swatch.color = Core.ShiftStyleUtility.ToColor(day.shiftColorHex); details.text = info.Describe(day);
            gameObject.SetActive(true); transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(100, scroll.rect.width - 56);
            float body = details.cachedTextGeneratorForLayout.GetPreferredHeight(details.text, details.GetGenerationSettings(new Vector2(width, 0))) / details.pixelsPerUnit;
            float height = Mathf.Clamp(body + 340, 470, ((RectTransform)transform).rect.height * .64f);
            card.pivot = new Vector2(.5f, 0);
            card.sizeDelta = new Vector2(card.sizeDelta.x, height);
            card.anchoredPosition = new Vector2(0, 160);
        }
        public void OpenDetails() { Hide(); editor.Show(selected, information); }
        public void Hide() => gameObject.SetActive(false);
    }
}
