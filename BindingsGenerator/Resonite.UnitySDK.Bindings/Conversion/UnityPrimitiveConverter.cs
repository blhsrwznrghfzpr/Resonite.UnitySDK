using ResoniteLink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public static class UnityPrimitiveConverter
{
    public static float2 ToResoniteLink(this Vector2 v) => new float2() { x = v.x, y = v.y };
    public static float3 ToResoniteLink(this Vector3 v) => new float3() { x = v.x, y = v.y, z = v.z };
    public static float4 ToResoniteLink(this Vector4 v) => new float4() { x = v.x, y = v.y, z = v.z, w = v.w };

    public static floatQ ToResoniteLink(this  Quaternion q) => new floatQ() { x = q.x, y = q.y, z = q.z, w = q.w };

    public static int2 ToResoniteLink(this Vector2Int v) => new int2() { x = v.x, y = v.y };
    public static int3 ToResoniteLink(this Vector3Int v) => new int3() { x = v.x, y = v.y, z = v.z };
    public static int4 ToResoniteLink(this Vector4Int v) => new int4() { x = v.x, y = v.y, z = v.z, w = v.w };

    public static float4x4 ToResoniteLink(this Matrix4x4 m) => new float4x4()
    {
        m00 = m.m00, m01 = m.m01, m02 = m.m02, m03 = m.m03,
        m10 = m.m10, m11 = m.m11, m12 = m.m12, m13 = m.m13,
        m20 = m.m20, m21 = m.m21, m22 = m.m22, m23 = m.m23,
        m30 = m.m30, m31 = m.m31, m32 = m.m32, m33 = m.m33,
    };

    public static color ToResoniteLink(this Color c) => new color() { r = c.r, g = c.g, b = c.b, a = c.a };
    public static color32 ToResoniteLink(this Color32 c) => new color32() { r = c.r, g = c.g, b = c.b, a = c.a };

    public static ResoniteLink.Rect ToResoniteLink(this UnityEngine.Rect r) => new ResoniteLink.Rect()
    {
        position = r.position.ToResoniteLink(),
        size = r.size.ToResoniteLink()
    };

    public static ResoniteLink.IntRect ToResoniteLink(this UnityEngine.RectInt r) => new ResoniteLink.IntRect()
    {
        position = r.position.ToResoniteLink(),
        size = r.size.ToResoniteLink()
    };

    public static ResoniteLink.BoundingBox ToResoniteLink(this UnityEngine.Bounds b) => new ResoniteLink.BoundingBox()
    {
        min = b.min.ToResoniteLink(),
        max = b.max.ToResoniteLink()
    };

    static float GetSHScale(int coefficientIndex)
    {
        switch(coefficientIndex)
        {
            case 0:
                return 0.2820947917f;

            case 1:
            case 2:
            case 3:
                return 0.4886025119f;

            case 4:
            case 5:
            case 7:
                return 1.0925484306f;

            case 6:
                return 0.3153915652f;

            case 8:
                return 0.5462742153f;

            default:
                throw new ArgumentOutOfRangeException(nameof(coefficientIndex));
        }
    }

    public static ResoniteLink.SphericalHarmonicsL2 ToResoniteLink(this UnityEngine.Rendering.SphericalHarmonicsL2 sh)
    {
        var link = new SphericalHarmonicsL2();

        colorX GetCoefficient(int index)
        {
            // Unity SH coefficients are pre-multiplied, so we need to compensate
            var scale = 1f / GetSHScale(index);

            var r = sh[0, index] * scale;
            var g = sh[0, index] * scale;
            var b = sh[0, index] * scale;

            return new colorX() { r = r, g = g, b = b, a = 1, Profile = "Linear" };
        }

        link.SH0 = new Data_colorX() { Value = GetCoefficient(0) };
        link.SH1 = new Data_colorX() { Value = GetCoefficient(1) };
        link.SH2 = new Data_colorX() { Value = GetCoefficient(2) };
        link.SH3 = new Data_colorX() { Value = GetCoefficient(3) };
        link.SH4 = new Data_colorX() { Value = GetCoefficient(4) };
        link.SH5 = new Data_colorX() { Value = GetCoefficient(5) };
        link.SH6 = new Data_colorX() { Value = GetCoefficient(6) };
        link.SH7 = new Data_colorX() { Value = GetCoefficient(7) };
        link.SH8 = new Data_colorX() { Value = GetCoefficient(8) };

        return link;
    }
}
