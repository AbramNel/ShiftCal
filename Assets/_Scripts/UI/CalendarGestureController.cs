using UnityEngine;
namespace ShiftCal.UI
{
    public class CalendarGestureController : MonoBehaviour
    {
        public CalendarController calendar;
        Vector2 start;
        float at;
        bool tracking;
        public void Begin(Vector2 position){start=position;at=Time.unscaledTime;tracking=!calendar.IsEditing;}
        public bool Finish(Vector2 position)
        {
            if(!tracking||calendar.IsEditing){tracking=false;return false;}tracking=false;var delta=position-start;float distance=Mathf.Abs(delta.x),elapsed=Mathf.Max(.016f,Time.unscaledTime-at);
            float threshold=Mathf.Clamp(Screen.width*.16f,48,100);
            if(distance<threshold||distance<Mathf.Abs(delta.y)*1.6f||elapsed>1.1f&&distance/elapsed<220)return false;
            if(delta.x<0)calendar.NextMonth();else calendar.PrevMonth();return true;
        }
    }
}
