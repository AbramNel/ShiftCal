using System;

namespace ShiftCal.Data
{
    [Serializable]
    public class ShiftTypeDefinitionData
    {
        public int id;
        public string name;
        public string colorHex;
        public string startTime;
        public string endTime;
        public float hours;
    }
}
