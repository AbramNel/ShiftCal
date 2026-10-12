using UnityEngine;
using UnityEngine.UI;
namespace ShiftCal.UI
{
    public class NumberStepper : MonoBehaviour
    {
        public InputField input;
        public int minimum=1,maximum=365;
        public void Less()=>Change(-1);
        public void More()=>Change(1);
        private void Change(int delta){int.TryParse(input.text,out int current);input.text=Mathf.Clamp(current+delta,minimum,maximum).ToString();}
    }
}
