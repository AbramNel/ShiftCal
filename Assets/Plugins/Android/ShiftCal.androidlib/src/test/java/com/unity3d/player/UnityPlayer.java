package com.unity3d.player;
// Test-only receiver for asynchronous bridge messages; never packaged in the app.
public final class UnityPlayer {
    public static volatile String method,value;
    public static void UnitySendMessage(String object,String name,String json){method=name;value=json;}
}
