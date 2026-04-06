using UnityEngine;

/// <summary>
/// Minimal fuzzy helpers: triangular membership, fuzzy AND/OR, defuzz by max.
/// </summary>
public static class FuzzyMath
{
    public static float Tri(float x, float left, float peak, float right)
    {
        if (x <= left || x >= right)
            return 0f;
        if (x < peak)
            return Mathf.InverseLerp(left, peak, x);
        return Mathf.InverseLerp(right, peak, x);
    }

    public static float Trap(float x, float left, float leftShoulder, float rightShoulder, float right)
    {
        if (x <= left || x >= right)
            return 0f;
        if (x < leftShoulder)
            return Mathf.InverseLerp(left, leftShoulder, x);
        if (x > rightShoulder)
            return Mathf.InverseLerp(right, rightShoulder, x);
        return 1f;
    }

    public static float And(params float[] terms)
    {
        if (terms == null || terms.Length == 0)
            return 0f;
        float m = 1f;
        for (int i = 0; i < terms.Length; i++)
            m = Mathf.Min(m, terms[i]);
        return m;
    }

    public static float Or(params float[] terms)
    {
        if (terms == null || terms.Length == 0)
            return 0f;
        float m = 0f;
        for (int i = 0; i < terms.Length; i++)
            m = Mathf.Max(m, terms[i]);
        return m;
    }
}
