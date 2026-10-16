using System;
using System.Reflection;
using UnityEditor;
// Pointer checks must use the Android portrait geometry, even in a desktop Editor.
public static class PortraitRuntimeView
{
    const string Key="ShiftCal.RuntimePortrait.";
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Type Type(string name)=>typeof(EditorWindow).Assembly.GetType("UnityEditor."+name,true);
    static object Sizes()=>Type("GameViewSizes").BaseType.GetProperty("instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
    static object Group(object sizes,string name)=>sizes.GetType().GetMethod("GetGroup",Instance).Invoke(sizes,new[]{Enum.Parse(Type("GameViewSizeGroupType"),name)});
    public static void Begin()
    {
        var sizes=Sizes();var current= sizes.GetType().GetProperty("currentGroupType",Instance)?.GetValue(sizes);
        string groupName=current?.ToString()??(EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android?"Android":"Standalone");var group=Group(sizes,groupName);var view=EditorWindow.GetWindow(Type("GameView"),false,"Game",false);
        var selected=view.GetType().GetProperty("selectedSizeIndex",Instance);SessionState.SetInt(Key+"previous",(int)selected.GetValue(view));SessionState.SetString(Key+"group",groupName);
        var constructor=Type("GameViewSize").GetConstructor(Instance,null,new[]{Type("GameViewSizeType"),typeof(int),typeof(int),typeof(string)},null);
        var size=constructor.Invoke(new[]{Enum.Parse(Type("GameViewSizeType"),"FixedResolution"),(object)393,851,"ShiftCal temporary portrait verification"});group.GetType().GetMethod("AddCustomSize",Instance).Invoke(group,new[]{size});
        int builtIn=(int)group.GetType().GetMethod("GetBuiltinCount",Instance).Invoke(group,null),custom=(int)group.GetType().GetMethod("GetCustomCount",Instance).Invoke(group,null);SessionState.SetInt(Key+"custom",custom-1);SessionState.SetBool(Key+"active",true);selected.SetValue(view,builtIn+custom-1);view.Repaint();
    }
    public static void Restore()
    {
        if(!SessionState.GetBool(Key+"active",false))return;SessionState.SetBool(Key+"active",false);var group=Group(Sizes(),SessionState.GetString(Key+"group","Standalone"));var view=EditorWindow.GetWindow(Type("GameView"),false,"Game",false);view.GetType().GetProperty("selectedSizeIndex",Instance).SetValue(view,SessionState.GetInt(Key+"previous",0));group.GetType().GetMethod("RemoveCustomSize",Instance).Invoke(group,new object[]{SessionState.GetInt(Key+"custom",0)});
    }
}
