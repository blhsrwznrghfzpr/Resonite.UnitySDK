using FrooxEngine;
using UnityEngine;

[MaterialConverter(true,
    "lilToon",
    "Hidden/lilToonCutout",
    "Hidden/lilToonTransparent",
    "Hidden/lilToonOutline",
    "Hidden/lilToonCutoutOutline",
    "Hidden/lilToonTransparentOutline",
    "Hidden/lilToonLite",
    "Hidden/lilToonLiteCutout",
    "Hidden/lilToonLiteTransparent",
    "Hidden/lilToonLiteOutline",
    "Hidden/lilToonLiteCutoutOutline",
    "Hidden/lilToonLiteTransparentOutline",
    "_lil/lilToonMulti",
    "_lil/lilToonMultiOutline",
    "_lil/lilToonMultiRefraction",
    "_lil/lilToonMultiFur",
    "_lil/lilToonMultiGem"
)]
public class LilToonConverter : ResoniteMaterialConverter
{
    public XiexeToonMaterialWrapper Toon;

    public override IAssetProvider<FrooxEngine.Material> UpdateConversion(UnityEngine.Material material, IConversionContext context)
    {
        if (Toon == null)
            Toon = gameObject.AddComponent<XiexeToonMaterialWrapper>();

        var data = Toon.Data;
        var shaderName = material.shader.name;

        data.RenderQueue = material.renderQueue;

        // Main texture and color
        data.MainTexture = context.GetITexture2D(material.GetTexture("_MainTex"));
        data.Color = material.GetColor("_Color").ToColorX_sRGB();
        data.MainTextureScale = material.mainTextureScale;
        data.MainTextureOffset = material.mainTextureOffset;

        // Blend mode based on shader variant
        if (shaderName.Contains("Transparent"))
            data.BlendMode = BlendMode.Alpha;
        else if (shaderName.Contains("Cutout"))
            data.BlendMode = BlendMode.Cutout;
        else
            data.BlendMode = BlendMode.Opaque;

        // Alpha cutoff
        if (material.HasProperty("_Cutoff"))
            data.AlphaClip = material.GetFloat("_Cutoff");

        // Culling
        if (material.HasProperty("_Cull"))
        {
            var cull = (int)material.GetFloat("_Cull");
            // lilToon: 0=Off, 1=Front, 2=Back; Resonite Culling: 0=Off, 1=Front, 2=Back
            data.Culling = (Culling)cull;
        }

        // Normal map
        if (material.HasProperty("_UseBumpMap") && material.GetFloat("_UseBumpMap") > 0.5f ||
            !material.HasProperty("_UseBumpMap"))
        {
            data.NormalMap = context.GetITexture2D(material.GetTexture("_BumpMap"));
            if (material.HasProperty("_BumpScale"))
                data.NormalScale = material.GetFloat("_BumpScale");
        }

        // Metallic and smoothness (only when reflection is enabled)
        if (material.HasProperty("_UseReflection") && material.GetFloat("_UseReflection") > 0.5f)
        {
            if (material.HasProperty("_Metallic"))
                data.Metallic = material.GetFloat("_Metallic");
            if (material.HasProperty("_Smoothness"))
                data.Glossiness = material.GetFloat("_Smoothness");
            if (material.HasProperty("_Reflectance"))
                data.Reflectivity = material.GetFloat("_Reflectance");
        }

        // Emission
        if (material.HasProperty("_UseEmission") && material.GetFloat("_UseEmission") > 0.5f)
        {
            data.EmissionColor = material.GetColor("_EmissionColor").ToColorX_sRGB();
            data.EmissionMap = context.GetITexture2D(material.GetTexture("_EmissionMap"));
        }
        else
        {
            data.EmissionColor = Color.black.ToColorX_sRGB();
        }

        // Shadow
        if (material.HasProperty("_UseShadow") && material.GetFloat("_UseShadow") > 0.5f)
        {
            if (material.HasProperty("_ShadowBorder"))
                data.ShadowSharpness = 1f - material.GetFloat("_ShadowBlur");
        }

        // Outline
        bool isOutlineVariant = shaderName.Contains("Outline");
        bool useOutline = material.HasProperty("_UseOutline") && material.GetFloat("_UseOutline") > 0.5f;

        if (isOutlineVariant || useOutline)
        {
            data.Outline = XiexeToonMaterial.OutlineStyle.Lit;

            if (material.HasProperty("_OutlineColor"))
                data.OutlineColor = material.GetColor("_OutlineColor").ToColorX_sRGB();
            if (material.HasProperty("_OutlineWidth"))
                data.OutlineWidth = material.GetFloat("_OutlineWidth");
            if (material.HasProperty("_OutlineWidthMask"))
                data.OutlineMask = context.GetITexture2D(material.GetTexture("_OutlineWidthMask"));
        }
        else
        {
            data.Outline = XiexeToonMaterial.OutlineStyle.None;
        }

        // MatCap
        if (material.HasProperty("_UseMatCap") && material.GetFloat("_UseMatCap") > 0.5f)
        {
            data.Matcap = context.GetITexture2D(material.GetTexture("_MatCapTex"));
            if (material.HasProperty("_MatCapColor"))
                data.MatcapTint = material.GetColor("_MatCapColor").ToColorX_sRGB();
        }

        // Rim lighting
        if (material.HasProperty("_UseRim") && material.GetFloat("_UseRim") > 0.5f)
        {
            data.RimColor = material.GetColor("_RimColor").ToColorX_sRGB();

            if (material.HasProperty("_RimBorder"))
                data.RimRange = material.GetFloat("_RimBorder");
            if (material.HasProperty("_RimBlur"))
                data.RimSharpness = 1f - material.GetFloat("_RimBlur");
            if (material.HasProperty("_RimFresnelPower"))
                data.RimIntensity = 1f;
        }

        return data;
    }

    public static float? EvaluateHeuristicConversion(UnityEngine.Material material)
    {
        // Detect lilToon materials by checking for characteristic properties
        if (material.shader.name.Contains("lilToon") || material.shader.name.Contains("liltoon"))
            return 1.0f;

        float score = 0f;

        // Check for lilToon-specific properties that are unlikely to appear in other shaders
        if (material.HasProperty("_MainTexHSVG")) score += 0.3f;
        if (material.HasProperty("_UseShadow")) score += 0.2f;
        if (material.HasProperty("_UseMatCap")) score += 0.2f;
        if (material.HasProperty("_UseRim")) score += 0.15f;
        if (material.HasProperty("_UseEmission")) score += 0.15f;

        return score >= 0.5f ? score : null;
    }
}
