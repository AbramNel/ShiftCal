using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShiftCal.App;
using ShiftCal.Core;
using ShiftCal.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Runs real startup/Update/LateUpdate/canvas cycles, in addition to static portrait checks.
[InitializeOnLoad]
public static class ShiftCalRuntimeChecks
{
    private const string Pending = "ShiftCal.UIRuntimeChecks.Pending";
    private static IEnumerator runner;
    private static int previousFrame = -1;
    static ShiftCalRuntimeChecks() => EditorApplication.playModeStateChanged += StateChanged;
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == previousFrame) return;
        previousFrame = Time.frameCount;
        try { if (runner != null && !runner.MoveNext()) EditorApplication.update -= Tick; }
        catch (Exception ex) { Debug.LogException(ex);EditorApplication.update -= Tick;Finish(false); }
    }
    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        SessionState.SetBool(Pending+".accountHad", PlayerPrefs.HasKey("ShiftCal.ActiveAccount.v3"));
        SessionState.SetString(Pending+".account", PlayerPrefs.GetString("ShiftCal.ActiveAccount.v3"));
        SessionState.SetBool(Pending+".themeHad", PlayerPrefs.HasKey(ThemeManager.PreferenceKey));
        SessionState.SetInt(Pending+".theme", PlayerPrefs.GetInt(ThemeManager.PreferenceKey));
        SessionState.SetBool(Pending+".alarmHad",PlayerPrefs.HasKey("ShiftCal.AlarmPreferences.v1"));
        SessionState.SetString(Pending+".alarm",PlayerPrefs.GetString("ShiftCal.AlarmPreferences.v1"));
        PlayerPrefs.SetString("ShiftCal.ActiveAccount.v3", "local");PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Calendar.unity");EditorApplication.EnterPlaymode();
    }
    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) { runner = new ShiftCalRuntimeProbe().Run();previousFrame = -1;EditorApplication.update += Tick; }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        SessionState.SetBool(Pending, false);
        if (SessionState.GetBool(Pending+".accountHad", false)) PlayerPrefs.SetString("ShiftCal.ActiveAccount.v3",SessionState.GetString(Pending+".account", ""));else PlayerPrefs.DeleteKey("ShiftCal.ActiveAccount.v3");
        if (SessionState.GetBool(Pending+".themeHad", false)) PlayerPrefs.SetInt(ThemeManager.PreferenceKey,SessionState.GetInt(Pending+".theme", 0));else PlayerPrefs.DeleteKey(ThemeManager.PreferenceKey);
        if(SessionState.GetBool(Pending+".alarmHad",false))PlayerPrefs.SetString("ShiftCal.AlarmPreferences.v1",SessionState.GetString(Pending+".alarm",""));else PlayerPrefs.DeleteKey("ShiftCal.AlarmPreferences.v1");
        PlayerPrefs.Save();
        bool passed=SessionState.GetBool(Pending+".passed",false);
        Debug.Log(passed ? "ShiftCal runtime UI checks passed" : "ShiftCal runtime UI checks FAILED");
        EditorApplication.Exit(passed ? 0 : 1);
    }
    public static void Finish(bool passed)
    {
        SessionState.SetBool(Pending+".passed",passed);EditorApplication.ExitPlaymode();
    }
}
