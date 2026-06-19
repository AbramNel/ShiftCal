using System;
using System.Collections.Generic;

namespace ShiftCal.Core
{
    public static class ShiftPatternUtility
    {
        public static List<int> ExpandBlocks(List<(int shiftType, int length)> blocks)
        {
            List<int> result = new List<int>();

            foreach (var block in blocks)
            {
                for (int i = 0; i < block.length; i++)
                    result.Add(block.shiftType);
            }

            return result;
        }

        public static int Resolve(List<int> pattern, string startDateKey, DateTime target)
        {
            if (pattern == null || pattern.Count == 0) return 0;
            if (string.IsNullOrEmpty(startDateKey)) return 0;

            DateTime start = DateKeyUtility.FromDateKey(startDateKey);
            int days = (target.Date - start.Date).Days;
            int index = Mod(days, pattern.Count);
            return pattern[index];
        }

        private static int Mod(int a, int b)
        {
            int r = a % b;
            return r < 0 ? r + b : r;
        }
    }
}
