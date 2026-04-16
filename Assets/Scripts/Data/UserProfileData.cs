using System;

namespace ShiftCal.Data
{
    [Serializable]
    public class UserProfileData
    {
        public string userId;
        public string displayName;
        public long createdAt;
        public long updatedAt;
    }
}
