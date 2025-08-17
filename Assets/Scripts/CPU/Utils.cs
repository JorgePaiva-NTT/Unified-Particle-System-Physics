using UnityEngine;

public static class Utils {
    public static float Length(float4 f) {
        return Vector4.Magnitude(new Vector4(f.x, f.y, f.z, f.w));
    }
    public static float Length(float3 f) {
        return Vector3.Magnitude(new Vector3(f.x, f.y, f.z));
    }
    public static float Dot(float4 x, float4 y) {
        return Vector4.Dot(new Vector4(x.x, x.y, x.z, x.w), new Vector4(y.y, y.y, y.z, y.w));
    }
    public static float Dot(float3 x, float3 y) {
        return Vector3.Dot(new Vector3(x.x, x.y, x.z), new Vector3(y.y, y.y, y.z));
    }
    public static int Max(float l, float r) {
        return (int)Mathf.Max(l, r);
    }
    public static int Min(float l, float r) {
        return (int)Mathf.Min(l, r);
    }
    public static int Floor(float l) {
        return Mathf.FloorToInt(l);
    }

    public static float3 Normalize(float3 v) {
        return float3.Make_float3(new Vector3(v.x, v.y, v.z).normalized);
    }
}