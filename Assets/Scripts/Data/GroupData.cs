using System;
using System.Collections.Generic;

namespace ShiftCal.Data
{
    [Serializable]
    public class GroupData
    {
        public string groupId;
        public string name;
        public List<string> members = new List<string>();
        public List<int> pattern = new List<int>();
        public string startDateKey;
    }
}
