using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftCal.UI
{
    public class DurationChoice : MonoBehaviour
    {
        public Dropdown choice;
        public InputField custom;
        public int[] minutes;
        public int maximum = 10080;
        public int minimum = 1;
        private bool hooked;
        public int Minutes
        {
            get
            {
                if (choice.value < minutes.Length) return minutes[choice.value];
                if (!int.TryParse(custom.text, out var n) || n < minimum || n > maximum) throw new ArgumentException("Enter " + minimum + "–" + maximum + " minutes.");
                return n;
            }
        }
        public void Set(int value)
        {
            Hook(); int index = Array.IndexOf(minutes, value);
            choice.SetValueWithoutNotify(index < 0 ? minutes.Length : index); custom.text = value.ToString(); UpdateSection();
        }
        private void OnEnable() => Hook();
        private void Hook() { if (hooked || choice == null) return; hooked = true; choice.onValueChanged.AddListener(_ => UpdateSection()); }
        public void UpdateSection() => custom.transform.parent.gameObject.SetActive(choice.value == minutes.Length);
    }
}
