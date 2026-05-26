using FrooxEngine;
using UnityEngine;

public static class MToon10XiexeConverter
{
    private const int MToon10AlphaModeMask = 1;
    private const int MToon10AlphaModeBlend = 2;

    private const int OutlineWidthModeNone = 0;
    private const int OutlineWidthModeWorld = 1;
    private const int OutlineWidthModeScreen = 2;

    public static IAssetProvider<FrooxEngine.Material> UpdateConversion(
        FrooxEngine.XiexeToonMaterial xiexe,
        UnityEngine.Material material,
        IConversionContext context)
    {
        xiexe.MainTexture = context.GetITexture2D(material.GetTexture("_MainTex"));
        xiexe.Color = material.GetColor("_Color").ToColorX_sRGB();
        xiexe.UseVertexColors = false;
        xiexe.BlendMode = GetBlendMode(material);
        xiexe.ZWrite = GetZWrite(material);
        xiexe.AlphaClip = material.GetFloat("_Cutoff");
        xiexe.MainTextureScale = material.GetTextureScale("_MainTex");
        xiexe.MainTextureOffset = material.GetTextureOffset("_MainTex");
        xiexe.Saturation = 1;
        xiexe.NormalMap = context.GetITexture2D(material.GetTexture("_BumpMap"));
        xiexe.NormalMapScale = material.GetTextureScale("_BumpMap");
        xiexe.NormalMapOffset = material.GetTextureOffset("_BumpMap");
        xiexe.NormalScale = material.GetFloat("_BumpScale");
        xiexe.Metallic = 0;
        xiexe.Glossiness = 0;
        xiexe.Reflectivity = 0;
        xiexe.MetallicGlossMap = null;
        xiexe.MetallicGlossMapScale = Vector2.one;
        xiexe.MetallicGlossMapOffset = Vector2.zero;
        UpdateEmission(xiexe, material, context);
        UpdateRim(xiexe, material);
        xiexe.SpecularIntensity = 0;
        xiexe.SpecularArea = 0.5f;
        xiexe.Matcap = context.GetITexture2D(material.GetTexture("_MatcapTex"));
        xiexe.MatcapTint = material.GetColor("_MatcapColor").ToColorX_sRGB();
        xiexe.OcclusionMap = null;
        xiexe.OcclusionMapScale = Vector2.one;
        xiexe.OcclusionMapOffset = Vector2.zero;
        xiexe.OcclusionColor = Color.black.ToColorX_sRGB();
        UpdateOutline(xiexe, material, context);
        UpdateShadowRamp(xiexe, material, context);
        xiexe.ThicknessMap = null;
        xiexe.ThicknessMapScale = Vector2.one;
        xiexe.ThicknessMapOffset = Vector2.zero;
        xiexe.SubsurfaceColor = Color.black.ToColorX_sRGB();
        xiexe.SubsurfaceDistortion = 1;
        xiexe.SubsurfacePower = 1;
        xiexe.SubsurfaceScale = 1;
        xiexe.OffsetFactor = 0;
        xiexe.OffsetUnits = 0;
        xiexe.Culling = GetCulling(material);
        xiexe.ColorMask = ColorMask.RGBA;
        xiexe.AlbedoUV = 0;
        xiexe.NormalUV = 0;
        xiexe.MetallicUV = 0;
        xiexe.ThicknessUV = 0;
        xiexe.OcclusionUV = 0;
        xiexe.EmissionUV = 0;
        xiexe.RenderQueue = material.renderQueue;

        return xiexe;
    }

    private static void UpdateEmission(FrooxEngine.XiexeToonMaterial xiexe, UnityEngine.Material material, IConversionContext context)
    {
        var emissionColor = material.GetColor("_EmissionColor");
        xiexe.EmissionColor = emissionColor.ToColorX_Linear();

        if (emissionColor.maxColorComponent <= 0)
        {
            xiexe.EmissionMap = null;
            xiexe.EmissionMapScale = Vector2.one;
            xiexe.EmissionMapOffset = Vector2.zero;
            return;
        }

        var emissionMap = material.GetTexture("_EmissionMap");
        if (emissionMap != null)
        {
            xiexe.EmissionMap = context.GetITexture2D(emissionMap);
            xiexe.EmissionMapScale = material.GetTextureScale("_EmissionMap");
            xiexe.EmissionMapOffset = material.GetTextureOffset("_EmissionMap");
        }
        else
        {
            xiexe.EmissionMap = context.GetITexture2D(UnityEngine.Texture2D.whiteTexture);
            xiexe.EmissionMapScale = Vector2.one;
            xiexe.EmissionMapOffset = Vector2.zero;
        }
    }

    private static void UpdateRim(FrooxEngine.XiexeToonMaterial xiexe, UnityEngine.Material material)
    {
        var rimColor = material.GetColor("_RimColor");
        xiexe.RimColor = rimColor.ToColorX_sRGB();
        xiexe.RimAlbedoTint = 0;
        xiexe.RimAttenuationEffect = material.GetFloat("_RimLightingMix");
        xiexe.RimIntensity = rimColor.maxColorComponent;
        xiexe.RimRange = 1 / Mathf.Max(material.GetFloat("_RimFresnelPower"), 0.001f);
        xiexe.RimThreshold = material.GetFloat("_RimLift");
        xiexe.RimSharpness = 0.5f;
    }

    private static void UpdateOutline(FrooxEngine.XiexeToonMaterial xiexe, UnityEngine.Material material, IConversionContext context)
    {
        if (GetOutlineWidthMode(material) == OutlineWidthModeNone ||
            material.GetFloat("_OutlineWidth") <= 0)
        {
            xiexe.Outline = XiexeToonMaterial.OutlineStyle.None;
            xiexe.OutlineWidth = 1;
            xiexe.OutlineColor = Color.black.ToColorX_sRGB();
            xiexe.OutlineAlbedoTint = false;
            xiexe.OutlineMask = null;
            return;
        }

        if (material.GetFloat("_OutlineLightingMix") >= 0.5f)
        {
            xiexe.Outline = XiexeToonMaterial.OutlineStyle.Lit;
        }
        else
        {
            xiexe.Outline = XiexeToonMaterial.OutlineStyle.Emissive;
        }

        xiexe.OutlineWidth = material.GetFloat("_OutlineWidth");
        xiexe.OutlineColor = material.GetColor("_OutlineColor").ToColorX_sRGB();
        xiexe.OutlineAlbedoTint = material.GetFloat("_OutlineLightingMix") >= 0.5f;
        xiexe.OutlineMask = context.GetITexture2D(material.GetTexture("_OutlineWidthTex"));
    }

    private static int GetOutlineWidthMode(UnityEngine.Material material)
    {
        if (material.IsKeywordEnabled("_MTOON_OUTLINE_WORLD"))
        {
            return OutlineWidthModeWorld;
        }
        if (material.IsKeywordEnabled("_MTOON_OUTLINE_SCREEN"))
        {
            return OutlineWidthModeScreen;
        }

        return Mathf.RoundToInt(material.GetFloat("_OutlineWidthMode"));
    }

    private static void UpdateShadowRamp(FrooxEngine.XiexeToonMaterial xiexe, UnityEngine.Material material, IConversionContext context)
    {
        xiexe.ShadowRamp = context.GetITexture2D(GenerateShadowRamp(material));
        xiexe.ShadowRampMask = context.GetITexture2D(material.GetTexture("_ShadingShiftTex"));
        xiexe.ShadowRampMaskOffset = material.GetTextureOffset("_ShadingShiftTex");
        xiexe.ShadowRampMaskScale = material.GetTextureScale("_ShadingShiftTex");
        xiexe.ShadowRim = Color.white.ToColorX_sRGB();
        xiexe.ShadowSharpness = 0;
        xiexe.ShadowRimRange = 0.7f;
        xiexe.ShadowRimThreshold = 0.1f;
        xiexe.ShadowRimSharpness = 0.3f;
        xiexe.ShadowRimAlbedoTint = 0;
    }

    private static UnityEngine.Texture GenerateShadowRamp(UnityEngine.Material material)
    {
        const int rampWidth = 256;
        const int rampHeight = 256;
        var ramp = new UnityEngine.Texture2D(rampWidth, rampHeight, TextureFormat.RGBA32, false)
        {
            name = $"MToon10ShadowRamp - {material.name}"
        };
        ramp.wrapModeU = TextureWrapMode.Clamp;
        ramp.wrapModeV = TextureWrapMode.Clamp;

        var shadowMultiplier = GetShadowMultiplier(material);
        var shadeShift = material.GetFloat("_ShadingShiftFactor");
        var shadeToony = Mathf.Clamp01(material.GetFloat("_ShadingToonyFactor"));
        var shadingShiftTexScale = material.GetFloat("_ShadingShiftTexScale");
        var shadeMin = -1 + shadeToony;
        var shadeMax = 1 - shadeToony;
        var shadeRange = Mathf.Max(shadeMax - shadeMin, 0.0001f);

        Color32[] pixels = new Color32[rampWidth * rampHeight];
        for (int y = 0; y < rampHeight; y++)
        {
            var mask = y / (rampHeight - 1f);
            var effectiveShadeShift = shadeShift + mask * shadingShiftTexScale;
            for (int x = 0; x < rampWidth; x++)
            {
                var dotNL = Mathf.Lerp(-1, 1, x / (rampWidth - 1f));
                var lit = Mathf.Clamp01((dotNL + effectiveShadeShift - shadeMin) / shadeRange);
                var pixel = Color.Lerp(shadowMultiplier, Color.white, lit);
                pixel.a = 1;
                pixels[rampWidth * y + x] = pixel;
            }
        }
        ramp.SetPixels32(pixels);
        ramp.Apply();
        return ramp;
    }

    private static Color GetShadowMultiplier(UnityEngine.Material material)
    {
        var baseColor = material.GetColor("_Color");
        var shadeColor = material.GetColor("_ShadeColor");
        return new(
            SafeColorRatio(shadeColor.r, baseColor.r),
            SafeColorRatio(shadeColor.g, baseColor.g),
            SafeColorRatio(shadeColor.b, baseColor.b),
            1);
    }

    private static float SafeColorRatio(float numerator, float denominator)
    {
        if (denominator <= 0.0001f)
        {
            return Mathf.Clamp01(numerator);
        }
        return Mathf.Clamp01(numerator / denominator);
    }


    private static BlendMode GetBlendMode(UnityEngine.Material material)
    {
        var alphaMode = Mathf.RoundToInt(material.GetFloat("_AlphaMode"));
        return alphaMode switch
        {
            MToon10AlphaModeMask => BlendMode.Cutout,
            MToon10AlphaModeBlend => BlendMode.Alpha,
            _ => BlendMode.Opaque,
        };
    }

    private static ZWrite GetZWrite(UnityEngine.Material material)
    {
        var alphaMode = Mathf.RoundToInt(material.GetFloat("_AlphaMode"));
        if (alphaMode != MToon10AlphaModeBlend)
        {
            return ZWrite.On;
        }

        return Mathf.RoundToInt(material.GetFloat("_TransparentWithZWrite")) > 0
            ? ZWrite.On
            : ZWrite.Off;
    }

    private static Culling GetCulling(UnityEngine.Material material)
    {
        if (material.GetFloat("_DoubleSided") > 0)
        {
            return Culling.Off;
        }

        return (Culling)(UnityEngine.Rendering.CullMode)Mathf.RoundToInt(material.GetFloat("_M_CullMode"));
    }
}
