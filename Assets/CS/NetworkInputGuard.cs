using UnityEngine;
/// <summary>Per-object token bucket. Excess requests are silently dropped, never a ban signal.</summary>
public sealed class RequestBudget {
 readonly float rate, capacity; float tokens; double last = -1;
 public RequestBudget(float perSecond, float burst) { rate=perSecond; capacity=burst; tokens=burst; }
 public bool Allow() { double now=Time.realtimeSinceStartupAsDouble;
 if(last>=0) tokens=Mathf.Min(capacity,tokens+(float)(now-last)*rate); last=now;
 if(tokens<1f)return false; tokens-=1f; return true; }
}
public static class NetworkInputGuard {
 public static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
 public static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
 public static bool Finite(Vector2 v)=>Finite(v.x)&&Finite(v.y);
 public static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
 public static bool ValidRotation(Quaternion q) {
 if(!Finite(q.x)||!Finite(q.y)||!Finite(q.z)||!Finite(q.w))return false;
 float n=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w; return n>0.9f&&n<1.1f; }
 public static bool Playing=>StageManager.Instance!=null&&StageManager.Instance.IsGameStarted&&!StageManager.Instance.RoomTimerFinished;
}
