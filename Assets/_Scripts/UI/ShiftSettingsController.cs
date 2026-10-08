using System.Collections.Generic;
using ShiftCal.Core;
using ShiftCal.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class ShiftSettingsController : MonoBehaviour
    {
        public ShiftEditor editor;
        [SerializeField] private List<ShiftSettingRow> rows = new List<ShiftSettingRow>();
        [SerializeField] private Text validationLabel;
        [SerializeField] private ShiftSettingRow rowPrefab;
        [SerializeField] private Transform rowContent;
        [SerializeField] private RectTransform settingsScroll;
        private void OnEnable() { if (App.AppSession.Instance != null) { App.AppSession.Instance.Changed += Refresh; Refresh(); } }
        private void OnDisable() { if (App.AppSession.Instance != null) App.AppSession.Instance.Changed -= Refresh; }

        private void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            GroupData group = ShiftCal.App.AppSession.Instance != null ? ShiftCal.App.AppSession.Instance.CurrentGroup : null;
            List<ShiftTypeDefinitionData> shiftTypes = group != null ? group.shiftTypes.FindAll(x=>!x.retired) : null;
            int shiftCount = shiftTypes != null ? shiftTypes.Count : 0;
            while (rows.Count < shiftCount + 1 && rowPrefab != null) rows.Add(Instantiate(rowPrefab, rowContent));

            for (int i = 0; i < rows.Count; i++)
            {
                bool addRow = i == shiftCount;
                ShiftTypeDefinitionData definition = shiftTypes != null && i < shiftTypes.Count ? shiftTypes[i] : null;
                rows[i].Bind(this, definition, addRow);
            }
        }

        public void SaveShiftRow(ShiftTypeDefinitionData definition, bool isAddRow, string title, string colorHex, string start, string end)
        {
            GroupData group = ShiftCal.App.AppSession.Instance != null ? ShiftCal.App.AppSession.Instance.CurrentGroup : null;
            if (group == null)
            {
                SetValidation("Calendar group is not ready.");
                return;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                SetValidation("Enter a shift title.");
                return;
            }

            if (!ShiftTimeUtility.TryCalculateHours(start, end, out float hours))
            {
                SetValidation("Use a time like 5:30 AM or 17:30.");
                return;
            }

            if (isAddRow || definition == null)
            {
                definition = new ShiftTypeDefinitionData { id = GetNextShiftId(group.shiftTypes) };
                group.shiftTypes.Add(definition);
            }

            definition.name = title;
            definition.colorHex = colorHex;
            definition.startTime = start;
            definition.endTime = end;
            definition.hours = hours;

            ShiftCal.App.AppSession.Instance?.SaveLocal();
            SetValidation(string.Empty);
            Refresh();
        }

        public void DeleteShift(ShiftTypeDefinitionData definition)
        {
            GroupData group = ShiftCal.App.AppSession.Instance != null ? ShiftCal.App.AppSession.Instance.CurrentGroup : null;
            if (group == null || definition == null || definition.id < 100)
                return;

            if (!App.AppSession.Instance.CanDeleteShift(definition.id, out string reason)) { SetValidation(reason); return; }
            definition.retired=true;
            ShiftCal.App.AppSession.Instance?.SaveLocal();
            Refresh();
        }

        private void SetValidation(string message)
        {
            if (validationLabel != null)
            {
                validationLabel.text = message;
                if(settingsScroll!=null)settingsScroll.offsetMin=new Vector2(settingsScroll.offsetMin.x,string.IsNullOrEmpty(message)?24:180);
            }
        }

        private static int GetNextShiftId(List<ShiftTypeDefinitionData> shiftTypes)
        {
            int next = 100;
            if (shiftTypes == null)
                return next;

            foreach (ShiftTypeDefinitionData shiftType in shiftTypes)
            {
                if (shiftType != null && shiftType.id >= next)
                    next = shiftType.id + 1;
            }

            return next;
        }
        public void InvalidColor()=>SetValidation("Use a hex color like #FBBF24.");
    }
}
