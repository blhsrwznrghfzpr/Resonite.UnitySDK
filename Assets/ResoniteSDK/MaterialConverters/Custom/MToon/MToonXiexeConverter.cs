using FrooxEngine;
using UnityEngine;

public interface IMToonXiexeConverter
{
    IAssetProvider<FrooxEngine.Material> UpdateConversion();
}

public class MToonXiexeConverter : IMToonXiexeConverter
{
    private const int MToon0RenderModeCutout = 1;
    private const int MToon0RenderModeTransparent = 2;
    private const int MToon0RenderModeTransparentWithZWrite = 3;

    private const int OutlineWidthModeNone = 0;
    private const int OutlineWidthModeWorld = 1;
    private const int OutlineWidthModeScreen = 2;

    private FrooxEngine.XiexeToonMaterial Xiexe;
    private UnityEngine.Material Material;
    private IConversionContext Context;

    public MToonXiexeConverter(
        FrooxEngine.XiexeToonMaterial Xiexe,
        UnityEngine.Material Material,
        IConversionContext Context)
    {
        this.Xiexe = Xiexe;
        this.Material = Material;
        this.Context = Context;
    }

    public IAssetProvider<FrooxEngine.Material> UpdateConversion()
    {

        Xiexe.MainTexture = Context.GetITexture2D(Material.GetTexture("_MainTex"));
        Xiexe.Color = Material.GetColor("_Color").ToColorX_sRGB();
        Xiexe.UseVertexColors = false;
        Xiexe.BlendMode = GetBlendMode();
        Xiexe.ZWrite = GetZWrite();
        Xiexe.AlphaClip = Material.GetFloat("_Cutoff");
        Xiexe.MainTextureScale = Material.GetTextureScale("_MainTex");
        Xiexe.MainTextureOffset = Material.GetTextureOffset("_MainTex");
        Xiexe.Saturation = 1;
        Xiexe.NormalMap = Context.GetITexture2D(Material.GetTexture("_BumpMap"));
        Xiexe.NormalMapScale = Material.GetTextureScale("_MainTex");
        Xiexe.NormalMapOffset = Material.GetTextureOffset("_MainTex");
        Xiexe.NormalScale = Material.GetFloat("_BumpScale");
        Xiexe.Metallic = 0;
        Xiexe.Glossiness = 0;
        Xiexe.Reflectivity = 0;
        Xiexe.MetallicGlossMap = null;
        Xiexe.MetallicGlossMapScale = Vector2.one;
        Xiexe.MetallicGlossMapOffset = Vector2.zero;
        UpdateEmission();
        UpdateRim();
        Xiexe.SpecularIntensity = 0;
        Xiexe.SpecularArea = 0.5f;
        Xiexe.Matcap = Context.GetITexture2D(Material.GetTexture("_SphereAdd"));
        Xiexe.MatcapTint = Color.white.ToColorX_sRGB();
        Xiexe.OcclusionMap = null;
        Xiexe.OcclusionMapScale = Vector2.one;
        Xiexe.OcclusionMapOffset = Vector2.zero;
        Xiexe.OcclusionColor = Color.black.ToColorX_sRGB();
        UpdateOutline();
        UpdateShadowRamp();
        Xiexe.ThicknessMap = null;
        Xiexe.ThicknessMapScale = Vector2.one;
        Xiexe.ThicknessMapOffset = Vector2.zero;
        Xiexe.SubsurfaceColor = Color.black.ToColorX_sRGB();
        Xiexe.SubsurfaceDistortion = 1;
        Xiexe.SubsurfacePower = 1;
        Xiexe.SubsurfaceScale = 1;
        Xiexe.OffsetFactor = 0;
        Xiexe.OffsetUnits = 0;
        Xiexe.Culling = GetCulling();
        Xiexe.ColorMask = ColorMask.RGBA;
        Xiexe.AlbedoUV = 0;
        Xiexe.NormalUV = 0;
        Xiexe.MetallicUV = 0;
        Xiexe.ThicknessUV = 0;
        Xiexe.OcclusionUV = 0;
        Xiexe.EmissionUV = 0;
        Xiexe.RenderQueue = Material.renderQueue;

        return Xiexe;
    }

    private void UpdateEmission()
    {
        var emissionColor = Material.GetColor("_EmissionColor");
        Xiexe.EmissionColor = emissionColor.ToColorX_Linear();

        if (emissionColor.maxColorComponent <= 0)
        {
            Xiexe.EmissionMap = null;
            Xiexe.EmissionMapScale = Vector2.one;
            Xiexe.EmissionMapOffset = Vector2.zero;
            return;
        }

        var emissionMap = Material.GetTexture("_EmissionMap");
        if (emissionMap != null)
        {
            Xiexe.EmissionMap = Context.GetITexture2D(emissionMap);
            Xiexe.EmissionMapOffset = Material.GetTextureOffset("_MainTex");
            Xiexe.EmissionMapScale = Material.GetTextureScale("_MainTex");
        }
        else
        {
            Xiexe.EmissionMap = Context.GetITexture2D(UnityEngine.Texture2D.whiteTexture);
            Xiexe.EmissionMapScale = Material.GetTextureScale("_MainTex");
            Xiexe.EmissionMapOffset = Material.GetTextureOffset("_MainTex");
        }
    }

    private void UpdateRim()
    {
        var rimColor = Material.GetColor("_RimColor");
        Xiexe.RimColor = rimColor.ToColorX_sRGB();
        Xiexe.RimAlbedoTint = 0;
        Xiexe.RimAttenuationEffect = Material.GetFloat("_RimLightingMix");
        Xiexe.RimIntensity = rimColor.maxColorComponent;
        Xiexe.RimRange = 1 / Mathf.Max(Material.GetFloat("_RimFresnelPower"), 0.001f);
        Xiexe.RimThreshold = Material.GetFloat("_RimLift");
        Xiexe.RimSharpness = 0.5f;
    }

    private void UpdateOutline()
    {
        if (GetOutlineWidthMode() == OutlineWidthModeNone ||
            Material.GetFloat("_OutlineWidth") <= 0)
        {
            Xiexe.Outline = XiexeToonMaterial.OutlineStyle.None;
            Xiexe.OutlineWidth = 1;
            Xiexe.OutlineColor = Color.black.ToColorX_sRGB();
            Xiexe.OutlineAlbedoTint = false;
            Xiexe.OutlineMask = null;
            return;
        }

        if (Material.GetFloat("_OutlineColorMode") > 0 || Material.GetFloat("_OutlineLightingMix") >= 0.5f)
        {
            Xiexe.Outline = XiexeToonMaterial.OutlineStyle.Lit;
        }
        else
        {
            Xiexe.Outline = XiexeToonMaterial.OutlineStyle.Emissive;
        }

        Xiexe.OutlineWidth = Material.GetFloat("_OutlineWidth");
        Xiexe.OutlineColor = Material.GetColor("_OutlineColor").ToColorX_Linear();
        Xiexe.OutlineAlbedoTint = Material.GetFloat("_OutlineLightingMix") >= 0.5f;
        Xiexe.OutlineMask = Context.GetITexture2D(Material.GetTexture("_OutlineWidthTexture"));
    }

    private int GetOutlineWidthMode()
    {
        if (Material.IsKeywordEnabled("MTOON_OUTLINE_WIDTH_WORLD"))
        {
            return OutlineWidthModeWorld;
        }
        if (Material.IsKeywordEnabled("MTOON_OUTLINE_WIDTH_SCREEN"))
        {
            return OutlineWidthModeScreen;
        }

        return Mathf.RoundToInt(Material.GetFloat("_OutlineWidthMode"));
    }

    private void UpdateShadowRamp()
    {
        Xiexe.ShadowRamp = Context.GetITexture2D(GenerateShadowRamp());
        Xiexe.ShadowRampMask = Context.GetITexture2D(Material.GetTexture("_ShadingGradeTexture") ?? UnityEngine.Texture2D.whiteTexture);
        Xiexe.ShadowRampMaskOffset = Material.GetTextureOffset("_MainTex");
        Xiexe.ShadowRampMaskScale = Material.GetTextureScale("_MainTex");
        Xiexe.ShadowRim = Color.white.ToColorX_sRGB();
        Xiexe.ShadowSharpness = 0;
        Xiexe.ShadowRimRange = 0.7f;
        Xiexe.ShadowRimThreshold = 0.1f;
        Xiexe.ShadowRimSharpness = 0.3f;
        Xiexe.ShadowRimAlbedoTint = 0;
    }

    private UnityEngine.Texture GenerateShadowRamp()
    {
        const int rampWidth = 256;
        const int rampHeight = 256;
        var ramp = new UnityEngine.Texture2D(rampWidth, rampHeight, TextureFormat.RGBA32, false)
        {
            name = $"MToonShadowRamp - {Material.name}"
        };
        ramp.wrapModeU = TextureWrapMode.Clamp;
        ramp.wrapModeV = TextureWrapMode.Clamp;

        var shadowMultiplier = GetShadowMultiplier();
        var shadeShift = Material.GetFloat("_ShadeShift");
        var shadeToony = Mathf.Clamp01(Material.GetFloat("_ShadeToony"));
        var shadingGradeRate = Material.GetFloat("_ShadingGradeRate");
        var shadeMin = shadeShift;
        var shadeMax = Mathf.Lerp(1, shadeShift, shadeToony);
        var shadeRange = Mathf.Max(shadeMax - shadeMin, 0.0001f);

        Color32[] pixels = new Color32[rampWidth * rampHeight];
        for (int y = 0; y < rampHeight; y++)
        {
            var mask = y / (rampHeight - 1f);
            var shadingGrade = 1 - shadingGradeRate * (1 - mask);
            for (int x = 0; x < rampWidth; x++)
            {
                var rampInput = x / (rampWidth - 1f);
                var lightIntensity = rampInput * shadingGrade * 2 - 1;
                var lit = Mathf.Clamp01((lightIntensity - shadeMin) / shadeRange);
                var pixel = Color.Lerp(shadowMultiplier, Color.white, lit);
                pixel.a = 1;
                pixels[rampWidth * y + x] = pixel;
            }
        }
        ramp.SetPixels32(pixels);
        ramp.Apply();
        return ramp;
    }

    private Color GetShadowMultiplier()
    {
        var baseColor = Material.GetColor("_Color");
        var shadeColor = Material.GetColor("_ShadeColor");
        return new(
            SafeColorRatio(shadeColor.r, baseColor.r),
            SafeColorRatio(shadeColor.g, baseColor.g),
            SafeColorRatio(shadeColor.b, baseColor.b),
            1);
    }

    private float SafeColorRatio(float numerator, float denominator)
    {
        if (denominator <= 0.0001f)
        {
            return Mathf.Clamp01(numerator);
        }
        return Mathf.Clamp01(numerator / denominator);
    }

    private BlendMode GetBlendMode()
    {
        var renderMode = Mathf.RoundToInt(Material.GetFloat("_BlendMode"));
        return renderMode switch
        {
            MToon0RenderModeCutout => BlendMode.Cutout,
            MToon0RenderModeTransparent => BlendMode.Alpha,
            MToon0RenderModeTransparentWithZWrite => BlendMode.Alpha,
            _ => BlendMode.Opaque,
        };
    }

    private ZWrite GetZWrite()
    {
        return Material.GetFloat("_ZWrite") > 0 ? ZWrite.On : ZWrite.Off;
    }

    private Culling GetCulling()
    {
        return (Culling)(UnityEngine.Rendering.CullMode)Mathf.RoundToInt(Material.GetFloat("_CullMode"));
    }
}
