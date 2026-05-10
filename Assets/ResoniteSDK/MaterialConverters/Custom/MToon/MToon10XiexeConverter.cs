using FrooxEngine;
using UnityEngine;

public class MToon10XiexeConverter : IMToonXiexeConverter
{
    private const int MToon10AlphaModeMask = 1;
    private const int MToon10AlphaModeBlend = 2;

    private const int OutlineWidthModeNone = 0;
    private const int OutlineWidthModeWorld = 1;
    private const int OutlineWidthModeScreen = 2;

    private FrooxEngine.XiexeToonMaterial Xiexe;
    private UnityEngine.Material Material;
    private IConversionContext Context;

    public MToon10XiexeConverter(
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
        Xiexe.NormalMapScale = Material.GetTextureScale("_BumpMap");
        Xiexe.NormalMapOffset = Material.GetTextureOffset("_BumpMap");
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
        Xiexe.Matcap = Context.GetITexture2D(Material.GetTexture("_MatcapTex"));
        Xiexe.MatcapTint = Material.GetColor("_MatcapColor").ToColorX_sRGB();
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
            Xiexe.EmissionMapScale = Material.GetTextureScale("_EmissionMap");
            Xiexe.EmissionMapOffset = Material.GetTextureOffset("_EmissionMap");
        }
        else
        {
            Xiexe.EmissionMap = Context.GetITexture2D(UnityEngine.Texture2D.whiteTexture);
            Xiexe.EmissionMapScale = Vector2.one;
            Xiexe.EmissionMapOffset = Vector2.zero;
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

        if (Material.GetFloat("_OutlineLightingMix") >= 0.5f)
        {
            Xiexe.Outline = XiexeToonMaterial.OutlineStyle.Lit;
        }
        else
        {
            Xiexe.Outline = XiexeToonMaterial.OutlineStyle.Emissive;
        }

        Xiexe.OutlineWidth = Material.GetFloat("_OutlineWidth");
        Xiexe.OutlineColor = Material.GetColor("_OutlineColor").ToColorX_sRGB();
        Xiexe.OutlineAlbedoTint = Material.GetFloat("_OutlineLightingMix") >= 0.5f;
        Xiexe.OutlineMask = Context.GetITexture2D(Material.GetTexture("_OutlineWidthTex"));
    }

    private int GetOutlineWidthMode()
    {
        if (Material.IsKeywordEnabled("_MTOON_OUTLINE_WORLD"))
        {
            return OutlineWidthModeWorld;
        }
        if (Material.IsKeywordEnabled("_MTOON_OUTLINE_SCREEN"))
        {
            return OutlineWidthModeScreen;
        }

        return Mathf.RoundToInt(Material.GetFloat("_OutlineWidthMode"));
    }

    private void UpdateShadowRamp()
    {
        Xiexe.ShadowRamp = Context.GetITexture2D(GenerateShadowRamp());
        Xiexe.ShadowRampMask = Context.GetITexture2D(Material.GetTexture("_ShadingShiftTex"));
        Xiexe.ShadowRampMaskOffset = Material.GetTextureOffset("_ShadingShiftTex");
        Xiexe.ShadowRampMaskScale = Material.GetTextureScale("_ShadingShiftTex");
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
            name = $"MToon10ShadowRamp - {Material.name}"
        };
        ramp.wrapModeU = TextureWrapMode.Clamp;
        ramp.wrapModeV = TextureWrapMode.Clamp;

        var shadowMultiplier = GetShadowMultiplier();
        var shadeShift = Material.GetFloat("_ShadingShiftFactor");
        var shadeToony = Mathf.Clamp01(Material.GetFloat("_ShadingToonyFactor"));
        var shadingShiftTexScale = Material.GetFloat("_ShadingShiftTexScale");
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
        var alphaMode = Mathf.RoundToInt(Material.GetFloat("_AlphaMode"));
        return alphaMode switch
        {
            MToon10AlphaModeMask => BlendMode.Cutout,
            MToon10AlphaModeBlend => BlendMode.Alpha,
            _ => BlendMode.Opaque,
        };
    }

    private ZWrite GetZWrite()
    {
        var alphaMode = Mathf.RoundToInt(Material.GetFloat("_AlphaMode"));
        if (alphaMode != MToon10AlphaModeBlend)
        {
            return ZWrite.On;
        }

        return Mathf.RoundToInt(Material.GetFloat("_TransparentWithZWrite")) > 0
            ? ZWrite.On
            : ZWrite.Off;
    }

    private Culling GetCulling()
    {
        if (Material.GetFloat("_DoubleSided") > 0)
        {
            return Culling.Off;
        }

        return (Culling)(UnityEngine.Rendering.CullMode)Mathf.RoundToInt(Material.GetFloat("_M_CullMode"));
    }
}
