using UnityEngine;

public class Util
{
    public static void Vec2Str(Vector3 vec, string tag = "")
    {
        Debug.LogWarning(string.Format("{0}=({1},{2},{3})", tag, vec.x, vec.y, vec.z));
    }
}