using System;
namespace ShiftCal.Firebase
{
    public enum SyncFailure { None, Offline, Permission, Authentication, Membership, Conflict, InvalidData, Other }
    public static class SyncDiagnostics
    {
        public static SyncFailure Classify(string code)
        {
            string value=(code??"").Replace("_","").Replace("-","").Replace(" ","").ToLowerInvariant();
            if(value.Contains("permissiondenied"))return SyncFailure.Permission;
            if(value.Contains("unauthenticated"))return SyncFailure.Authentication;
            if(value.Contains("unavailable")||value.Contains("deadlineexceeded")||value.Contains("network")||value.Contains("offline"))return SyncFailure.Offline;
            if(value.Contains("aborted")||value.Contains("conflict"))return SyncFailure.Conflict;
            if(value.Contains("invalidargument")||value.Contains("outofrange")||value.Contains("dataloss"))return SyncFailure.InvalidData;
            return SyncFailure.Other;
        }
        public static string Guidance(SyncFailure failure)
        {
            switch(failure)
            {
                case SyncFailure.Offline:return "Offline · edits are saved on this device and will retry when connected.";
                case SyncFailure.Permission:return "Sync needs attention · cloud access denied. Edits are saved on this device. Check calendar membership and ask the project owner to verify the deployed ShiftCal rules.";
                case SyncFailure.Authentication:return "Sync needs attention · reconnect your Google account to resume sharing. Your saved calendar and pending edits are retained.";
                case SyncFailure.Membership:return "Sync needs attention · this account has no membership in this calendar. Ask its owner for an invitation. Your local calendar and pending edits are retained.";
                case SyncFailure.Conflict:return "Sync needs attention · another device changed this record. Review conflicting edits in Data & Sharing.";
                case SyncFailure.InvalidData:return "Sync needs attention · a saved edit needs review before it can be shared. Your local data and pending edits are retained.";
                default:return "Sync needs attention · sharing could not finish. Your edits are saved on this device. Retry when ready.";
            }
        }
        public static bool SupportedRecord(string key)
        {
            if(string.IsNullOrEmpty(key))return false;int slash=key.IndexOf('/');if(slash<=0||slash==key.Length-1||key.IndexOf('/',slash+1)>=0)return false;
            switch(key.Substring(0,slash)){case "rotation":case "shift":case "override":case "event":case "exception":case "activity":case "activityException":case "familyProfile":case "activityTemplate":return true;default:return false;}
        }
    }
}
