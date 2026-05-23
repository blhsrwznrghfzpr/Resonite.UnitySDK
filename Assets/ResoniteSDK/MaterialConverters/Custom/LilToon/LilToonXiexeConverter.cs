using System;
using System.Reflection;
using FrooxEngine;
using UnityEditor;
using UnityEngine;
using UnityColor = UnityEngine.Color;
using UnityMaterial = UnityEngine.Material;
using UnityObject = UnityEngine.Object;
using UnityRenderTexture = UnityEngine.RenderTexture;
using UnityRenderTextureFormat = UnityEngine.RenderTextureFormat;
using UnityRenderTextureReadWrite = UnityEngine.RenderTextureReadWrite;
using UnityShader = UnityEngine.Shader;
using UnityTexture = UnityEngine.Texture;
using UnityTexture2D = UnityEngine.Texture2D;

public class LilToonXiexeConverter
{
    private readonly XiexeToonMaterial Xiexe;
    private readonly UnityMaterial Material;
    private readonly IConversionContext Context;
    private readonly LilToonAssetCache AssetCache;

    public LilToonXiexeConverter(XiexeToonMaterial xiexe, UnityMaterial material, IConversionContext context, LilToonAssetCache assetCache)
    {
        Xiexe = xiexe;
        Material = material;
        Context = context;
        AssetCache = assetCache;
    }

    public IAssetProvider<FrooxEngine.Material> UpdateConversion()
    {
        var mainTextureBake = BakeMainTextureWithLilToon();
        var mainTexture = mainTextureBake.Texture ?? GetTexture("_MainTex");
        Xiexe.MainTexture = Context.GetITexture2D(mainTexture);
        Xiexe.Color = mainTextureBake.BakedWithColor ? UnityColor.white.ToColorX_sRGB() : GetColor("_Color", UnityColor.white).ToColorX_sRGB();
        Xiexe.BlendMode = GetBlendMode();
        Xiexe.ZWrite = GetFloat("_ZWrite", 1) > 0 ? ZWrite.On : ZWrite.Off;
        Xiexe.AlphaClip = GetFloat("_Cutoff", 0.5f);
        Xiexe.MainTextureScale = GetTextureScale("_MainTex");
        Xiexe.MainTextureOffset = GetTextureOffset("_MainTex");
        Xiexe.Saturation = mainTextureBake.Texture != null ? 1 : GetVector("_MainTexHSVG", new Vector4(0, 1, 1, 1)).y;
        Xiexe.NormalMap = Context.GetITexture2D(GetTexture("_BumpMap"));
        Xiexe.NormalMapScale = GetTextureScale("_BumpMap");
        Xiexe.NormalMapOffset = GetTextureOffset("_BumpMap");
        Xiexe.NormalScale = GetFloat("_BumpScale", 1);
        UpdateReflections();
        UpdateEmission(mainTexture);
        UpdateRim();
        UpdateMatcap();
        UpdateOcclusion();
        UpdateOutline();
        Xiexe.ShadowRamp = Context.GetITexture2D(GetTexture("_Ramp") ?? BakeShadowRampWithLilToon());
        // XiexeToon's null ShadowRampMask behaves differently from lilToon's null
        // _ShadowStrengthMask, which means no shadow strength mask. Preserve that with white.
        Xiexe.ShadowRampMask = Context.GetITexture2D(GetTexture("_ShadowStrengthMask") ?? UnityTexture2D.whiteTexture);
        Xiexe.ShadowRampMaskScale = GetTextureScale("_ShadowStrengthMask");
        Xiexe.ShadowRampMaskOffset = GetTextureOffset("_ShadowStrengthMask");
        Xiexe.ShadowSharpness = 1 - GetFloat("_ShadowBlur", 0.1f);
        UpdateShadowRim();
        Xiexe.Culling = (Culling)(UnityEngine.Rendering.CullMode)GetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        Xiexe.ColorMask = (ColorMask)GetFloat("_ColorMask", (float)ColorMask.RGBA);
        Xiexe.RenderQueue = Material.renderQueue;

        return Xiexe;
    }

    private BlendMode GetBlendMode()
    {
        var shaderName = Material.shader?.name;
        // Hidden/lilToonRefraction and Hidden/lilToonRefractionBlur use GrabPass
        // with Blend One Zero, so blend factors alone would make them look opaque.
        if (!string.IsNullOrEmpty(shaderName) && shaderName.Contains("Refraction"))
        {
            return BlendMode.Alpha;
        }

        var srcBlend = GetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        var dstBlend = GetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);

        if (dstBlend == (float)UnityEngine.Rendering.BlendMode.Zero)
        {
            return GetFloat("_AlphaToMask", 0) > 0 ? BlendMode.Cutout : BlendMode.Opaque;
        }

        if (dstBlend == (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)
        {
            return BlendMode.Alpha;
        }

        if (dstBlend == (float)UnityEngine.Rendering.BlendMode.One)
        {
            return BlendMode.Additive;
        }

        if (srcBlend == (float)UnityEngine.Rendering.BlendMode.DstColor)
        {
            return BlendMode.Multiply;
        }

        return BlendMode.Alpha;
    }

    private void UpdateReflections()
    {
        if (GetFloat("_UseReflection", 0) == 0)
        {
            Xiexe.MetallicGlossMap = null;
            Xiexe.Metallic = 0;
            Xiexe.Glossiness = 0;
            Xiexe.Reflectivity = 1;
            Xiexe.SpecularIntensity = 0;
            return;
        }

        var metallicGlossMap = GetTexture("_MetallicGlossMap");
        var smoothnessTexture = GetTexture("_SmoothnessTex");
        var xiexeMetallicGlossMap = smoothnessTexture != null
            ? BakeMetallicGlossMapForXiexe(metallicGlossMap, smoothnessTexture)
            : metallicGlossMap;
        var metallicGlossMapProcessor = metallicGlossMap != null && smoothnessTexture == null
            ? MetallicOnlyProcessor
            : null;
        var metallicGlossTransformProperty = metallicGlossMap != null ? "_MetallicGlossMap" : "_SmoothnessTex";

        Xiexe.MetallicGlossMap = Context.GetITexture2D(xiexeMetallicGlossMap, metallicGlossMapProcessor);
        Xiexe.MetallicGlossMapScale = GetTextureScale(metallicGlossTransformProperty);
        Xiexe.MetallicGlossMapOffset = GetTextureOffset(metallicGlossTransformProperty);
        Xiexe.Metallic = GetFloat("_Metallic", 0);
        Xiexe.Reflectivity = GetFloat("_Reflectance", 0.04f);
        var reflectionColor = GetColor("_ReflectionColor", UnityColor.white);
        Xiexe.SpecularIntensity = GetFloat("_ApplySpecular", 1) > 0
            ? 100 * GetFloat("_Reflectance", 0.04f) * reflectionColor.a
            : 0;
        // lilToon uses Smoothness for both direct specular and optional environment
        // reflections. Xiexe separates those concerns: SpecularArea shapes direct
        // specular, while Glossiness affects indirect/environment reflection roughness.
        Xiexe.SpecularArea = GetXiexeSpecularArea();
        // _ApplyReflection is lilToon's "Environment Reflections" toggle. Keep
        // Glossiness at 0 when it is disabled so Xiexe does not add a sharp
        // environment reflection to materials that only asked for direct specular.
        Xiexe.Glossiness = GetFloat("_ApplyReflection", 0) > 0
            ? GetFloat("_Smoothness", 0.5f)
            : 0;
    }

    private float GetXiexeSpecularArea()
    {
        var smoothness = GetFloat("_Smoothness", 0.5f);
        if (GetFloat("_SpecularToon", 0) == 0)
        {
            return Mathf.Clamp01(smoothness);
        }

        // lilToon toon specular thresholds pow(N.H, 1 / roughness) with
        // _SpecularBorder. Xiexe has one smoothness-like direct specular control,
        // so use the tighter of Smoothness and the inverse border. Averaging these
        // values makes low-border materials such as PLUSONE/Platinum Body too broad.
        return Mathf.Clamp01(Mathf.Max(smoothness, 1 - GetFloat("_SpecularBorder", 0.5f)));
    }

    private void UpdateRim()
    {
        Xiexe.RimThreshold = GetFloat("_RimDirStrength", 0);

        if (GetFloat("_UseRim", 0) == 0)
        {
            Xiexe.RimColor = Color.black.ToColorX_sRGB();
            Xiexe.RimAlbedoTint = 0;
            Xiexe.RimAttenuationEffect = 0;
            Xiexe.RimIntensity = 0;
            return;
        }

        var rimColor = GetColor("_RimColor", UnityColor.white);
        Xiexe.RimColor = rimColor.ToColorX_Auto();
        Xiexe.RimAlbedoTint = GetFloat("_RimMainStrength", 0);
        Xiexe.RimAttenuationEffect = GetFloat("_RimShadowMask", 0.5f);
        Xiexe.RimIntensity = rimColor.a;

        var rimShape = GetXiexeRimShape("_RimBorder", "_RimBlur", "_RimFresnelPower");
        Xiexe.RimRange = rimShape.x;
        Xiexe.RimSharpness = rimShape.y;
    }

    private void UpdateMatcap()
    {
        // Xiexe only supports additive MatCap and has no equivalent to lilToon's
        // _MatCapBlendMask. Applying unsupported modes or an unmasked MatCap changes
        // the look, so only convert additive, unmasked MatCap.
        if (GetFloat("_UseMatCap", 0) == 0 ||
            GetFloat("_MatCapBlendMode", 1) != 1 ||
            GetTexture("_MatCapBlendMask") != null)
        {
            Xiexe.Matcap = null;
            Xiexe.MatcapTint = Color.black.ToColorX_sRGB();
            return;
        }

        Xiexe.Matcap = Context.GetITexture2D(GetTexture("_MatCapTex"), OpacifyProcessor);
        var matcapColor = GetColor("_MatCapColor", UnityColor.white);
        var alpha = matcapColor.a;
        matcapColor *= GetFloat("_MatCapBlend", 1) * alpha;
        matcapColor.a = alpha;
        Xiexe.MatcapTint = matcapColor.ToColorX_Auto();
    }

    private static readonly AssetMessagePostProcessor OpacifyProcessor = TexturePostProcessing.ProcessPixels(Opacify);
    private static readonly AssetMessagePostProcessor MetallicOnlyProcessor = TexturePostProcessing.ProcessPixels(MetallicOnly);

    private static ResoniteLink.color Opacify(ResoniteLink.color c)
    {
        return new ResoniteLink.color()
        {
            r = c.r * c.a,
            g = c.g * c.a,
            b = c.b * c.a,
            a = c.a,
        };
    }

    private static ResoniteLink.color MetallicOnly(ResoniteLink.color c)
    {
        return new ResoniteLink.color()
        {
            r = c.r,
            g = 0,
            b = 0,
            a = 1,
        };
    }

    private void UpdateEmission(UnityTexture albedoTexture)
    {
        if (GetFloat("_UseEmission", 0) == 0)
        {
            Xiexe.EmissionMap = null;
            Xiexe.EmissionColor = Color.black.ToColorX_sRGB();
            return;
        }

        var emissionColor = GetColor("_EmissionColor", UnityColor.clear);
        var emissionBlend = GetFloat("_EmissionBlend", 1);
        var emissionMainStrength = Mathf.Clamp01(GetFloat("_EmissionMainStrength", 0));
        var albedoTint = UnityColor.Lerp(UnityColor.white, GetColor("_Color", UnityColor.white), emissionMainStrength);
        emissionColor.r *= albedoTint.r;
        emissionColor.g *= albedoTint.g;
        emissionColor.b *= albedoTint.b;
        emissionColor = ApplyEmissionFluorescenceApproximation(emissionColor);
        Xiexe.EmissionColor = emissionColor.ToColorX_Auto();

        var emissionMap = GetTexture("_EmissionMap");
        var emissionBlendMask = GetTexture("_EmissionBlendMask");
        var hasActiveEmission = emissionBlend > 0 && emissionColor.a > 0 && emissionColor.maxColorComponent > 0;
        var outputEmissionMap = hasActiveEmission ? GetEmissionMap(emissionMap, emissionBlendMask, albedoTexture) : null;
        Xiexe.EmissionMap = Context.GetITexture2D(outputEmissionMap);
        Xiexe.EmissionMapScale = GetEmissionMapScale(emissionMap, emissionBlendMask);
        Xiexe.EmissionMapOffset = GetEmissionMapOffset(emissionMap, emissionBlendMask);
        Xiexe.EmissionUV = emissionMap != null ? (int)GetFloat("_EmissionMap_UVMode", 0) : 0;
    }

    private UnityColor ApplyEmissionFluorescenceApproximation(UnityColor emissionColor)
    {
        var fluorescence = Mathf.Clamp01(GetFloat("_EmissionFluorescence", 0));
        if (fluorescence == 0)
        {
            return emissionColor;
        }

        // lilToon multiplies fluorescence emission by a lighting-dependent
        // invLighting value. Xiexe emission is static, so use the maximum-ish
        // value of lilToon's curve as a conservative approximation.
        var fluorescenceScale = Mathf.Lerp(1, 0.375f, fluorescence);
        emissionColor.r *= fluorescenceScale;
        emissionColor.g *= fluorescenceScale;
        emissionColor.b *= fluorescenceScale;
        return emissionColor;
    }

    private void UpdateOcclusion()
    {
        Xiexe.OcclusionMap = Context.GetITexture2D(GetTexture("_ShadowBorderMask"));
        Xiexe.OcclusionMapScale = GetTextureScale("_ShadowBorderMask");
        Xiexe.OcclusionMapOffset = GetTextureOffset("_ShadowBorderMask");

        if (Material.HasProperty("_UseShadow") && Material.GetFloat("_UseShadow") == 0)
        {
            Xiexe.OcclusionColor = Color.black.ToColorX_sRGB();
            return;
        }

        var shadowColor = GetColor("_ShadowColor", new UnityColor(0.82f, 0.76f, 0.85f, 1));
        shadowColor.a = 1;
        var occlusionColor = UnityColor.Lerp(UnityColor.white, shadowColor, GetFloat("_ShadowStrength", 1));
        Xiexe.OcclusionColor = occlusionColor.ToColorX_Auto();
    }

    private UnityTexture GetEmissionMap(UnityTexture emissionMap, UnityTexture emissionBlendMask, UnityTexture albedoTexture)
    {
        if (emissionBlendMask != null || GetFloat("_EmissionMainStrength", 0) > 0)
        {
            return BakeEmissionMapWithLilToon(emissionMap, emissionBlendMask, albedoTexture) ?? emissionMap ?? emissionBlendMask;
        }

        return emissionMap ?? UnityTexture2D.whiteTexture;
    }

    private Vector2 GetEmissionMapScale(UnityTexture emissionMap, UnityTexture emissionBlendMask)
    {
        if (emissionMap != null)
        {
            return GetTextureScale("_EmissionMap");
        }

        return emissionBlendMask != null ? GetTextureScale("_EmissionBlendMask") : Vector2.one;
    }

    private Vector2 GetEmissionMapOffset(UnityTexture emissionMap, UnityTexture emissionBlendMask)
    {
        if (emissionMap != null)
        {
            return GetTextureOffset("_EmissionMap");
        }

        return emissionBlendMask != null ? GetTextureOffset("_EmissionBlendMask") : Vector2.zero;
    }

    private void UpdateOutline()
    {
        if (!IsOutlineEnabled())
        {
            Xiexe.Outline = XiexeToonMaterial.OutlineStyle.None;
            Xiexe.OutlineMask = null;
            return;
        }

        Xiexe.Outline = GetFloat("_OutlineEnableLighting", 1) > 0
            ? XiexeToonMaterial.OutlineStyle.Lit
            : XiexeToonMaterial.OutlineStyle.Emissive;
        Xiexe.OutlineWidth = GetFloat("_OutlineWidth", 0.08f);
        Xiexe.OutlineColor = GetColor("_OutlineColor", UnityColor.white).ToColorX_Auto();
        Xiexe.OutlineAlbedoTint = GetFloat("_OutlineLitApplyTex", 0) > 0;
        Xiexe.OutlineMask = Context.GetITexture2D(GetTexture("_OutlineWidthMask"));
    }

    private bool IsOutlineEnabled()
    {
        if (GetFloat("_UseOutline", 0) != 0)
        {
            return true;
        }

        var shaderName = Material.shader != null ? Material.shader.name : string.Empty;
        return shaderName.IndexOf("Outline", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void UpdateShadowRim()
    {
        if (GetFloat("_UseRimShade", 0) == 0)
        {
            Xiexe.ShadowRim = Color.white.ToColorX_sRGB();
            Xiexe.ShadowRimThreshold = 1;
            Xiexe.ShadowRimRange = 0.7f;
            Xiexe.ShadowRimSharpness = 0.3f;
            Xiexe.ShadowRimAlbedoTint = 0;
            return;
        }

        var rimShadeColor = GetColor("_RimShadeColor", new UnityColor(0.5f, 0.5f, 0.5f, 1));
        var shadowRim = UnityColor.Lerp(UnityColor.white, rimShadeColor, rimShadeColor.a);
        shadowRim.a = 1;
        Xiexe.ShadowRim = shadowRim.ToColorX_Auto();
        Xiexe.ShadowRimThreshold = 0;
        Xiexe.ShadowRimAlbedoTint = 0;

        var rimShadeShape = GetXiexeRimShape("_RimShadeBorder", "_RimShadeBlur", "_RimShadeFresnelPower");
        Xiexe.ShadowRimRange = rimShadeShape.x;
        Xiexe.ShadowRimSharpness = rimShadeShape.y;
    }

    private MainTextureBakeResult BakeMainTextureWithLilToon()
    {
        var useMain2ndTexture = UsesMainLayer("_UseMain2ndTex");
        var useMain3rdTexture = UsesMainLayer("_UseMain3rdTex");
        var main2ndTexture = GetTexture("_Main2ndTex");
        var main3rdTexture = GetTexture("_Main3rdTex");
        var main2ndUsesUv0 = GetFloat("_Main2ndTex_UVMode", 0) == 0;
        var main3rdUsesUv0 = GetFloat("_Main3rdTex_UVMode", 0) == 0;
        var canBakeMain2ndTexture = useMain2ndTexture && (main2ndUsesUv0 || main2ndTexture == null);
        var canBakeMain3rdTexture = useMain3rdTexture && (main3rdUsesUv0 || main3rdTexture == null);

        var shouldBakeMain = GetVector("_MainTexHSVG", new Vector4(0, 1, 1, 1)) != new Vector4(0, 1, 1, 1)
            || GetFloat("_MainGradationStrength", 0) != 0
            || canBakeMain2ndTexture
            || canBakeMain3rdTexture;
        var shouldBakeAlpha = GetFloat("_AlphaMaskMode", 0) != 0 && GetTexture("_AlphaMask") != null;

        if (!shouldBakeMain && !shouldBakeAlpha)
        {
            return default;
        }

        var bakerShader = UnityShader.Find("Hidden/ltsother_baker");
        if (bakerShader == null)
        {
            UnityEngine.Debug.LogWarning("Could not find lilToon main texture baker shader Hidden/ltsother_baker.");
            return default;
        }

        var sourceTexture = GetTexture("_MainTex") ?? UnityTexture2D.whiteTexture;
        var sourceTexture2D = sourceTexture as UnityTexture2D ?? UnityTexture2D.whiteTexture;

        UnityTexture2D bakedTexture = null;
        UnityMaterial bakerMaterial = null;

        try
        {
            bakerMaterial = new UnityMaterial(bakerShader);
            bakerMaterial.CopyPropertiesFromMaterial(Material);
            SetColorFromSource(bakerMaterial, "_Color", UnityColor.white);
            SetColorFromSource(bakerMaterial, "_Color2nd", UnityColor.white);
            SetColorFromSource(bakerMaterial, "_Color3rd", UnityColor.white);

            SetFallbackTexture(bakerMaterial, "_MainTex", sourceTexture);
            SetFallbackTexture(bakerMaterial, "_MainGradationTex", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_MainColorAdjustMask", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_Main2ndTex", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_Main2ndBlendMask", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_Main3rdTex", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_Main3rdBlendMask", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_AlphaMask", UnityTexture2D.whiteTexture);

            // Non-UV0 2nd/3rd textures cannot be preserved in a single UV0 bake.
            // Null layer textures still bake as white, matching lilToon's editor baker.
            if (useMain2ndTexture && !canBakeMain2ndTexture)
            {
                bakerMaterial.SetFloat("_UseMain2ndTex", 0);
            }

            if (useMain3rdTexture && !canBakeMain3rdTexture)
            {
                bakerMaterial.SetFloat("_UseMain3rdTex", 0);
            }

            if (shouldBakeMain)
            {
                bakedTexture = BakeTextureWithLilToon(sourceTexture2D, bakerMaterial, "LilToonConverter baked main texture");
            }

            if (shouldBakeAlpha)
            {
                var alphaSourceTexture = bakedTexture ?? sourceTexture2D;
                bakerMaterial.EnableKeyword("_ALPHAMASK");
                bakerMaterial.SetTexture("_MainTex", alphaSourceTexture);

                var alphaBakedTexture = BakeTextureWithLilToon(alphaSourceTexture, bakerMaterial, "LilToonConverter baked main texture with alpha mask");
                if (bakedTexture != null)
                {
                    UnityObject.DestroyImmediate(bakedTexture);
                }
                bakedTexture = alphaBakedTexture;
            }
        }
        catch (Exception exception)
        {
            if (bakedTexture != null)
            {
                UnityObject.DestroyImmediate(bakedTexture);
            }

            UnityEngine.Debug.LogWarning($"Could not bake lilToon main texture. {exception.Message}");
            return default;
        }
        finally
        {
            if (bakerMaterial != null)
            {
                UnityObject.DestroyImmediate(bakerMaterial);
            }
        }

        if (bakedTexture == null)
        {
            return default;
        }

        return new MainTextureBakeResult(CacheBakedTexture(bakedTexture, sourceTexture2D, ref AssetCache.MainTexture), shouldBakeMain);
    }

    private readonly struct MainTextureBakeResult
    {
        public MainTextureBakeResult(UnityTexture texture, bool bakedWithColor)
        {
            Texture = texture;
            BakedWithColor = bakedWithColor;
        }

        public UnityTexture Texture { get; }
        public bool BakedWithColor { get; }
    }

    private bool UsesMainLayer(string useProperty)
    {
        return GetFloat(useProperty, 0) != 0;
    }

    private UnityTexture BakeMetallicGlossMapForXiexe(UnityTexture metallicGlossMap, UnityTexture smoothnessTexture)
    {
        var referenceTexture = metallicGlossMap as UnityTexture2D
            ?? smoothnessTexture as UnityTexture2D;
        var width = referenceTexture?.width ?? 4;
        var height = referenceTexture?.height ?? 4;
        UnityTexture2D bakedTexture = null;

        try
        {
            var metallicPixels = ReadTexturePixels(metallicGlossMap, width, height, UnityColor.white);
            var smoothnessPixels = ReadTexturePixels(smoothnessTexture, width, height, UnityColor.white);
            var outputPixels = new UnityColor[width * height];

            for (var i = 0; i < outputPixels.Length; i++)
            {
                outputPixels[i] = new UnityColor(metallicPixels[i].r, 0, 0, smoothnessPixels[i].r);
            }

            bakedTexture = new UnityTexture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "LilToonConverter metallic smoothness map",
                wrapMode = referenceTexture?.wrapMode ?? TextureWrapMode.Repeat,
                filterMode = referenceTexture?.filterMode ?? FilterMode.Bilinear,
                anisoLevel = referenceTexture?.anisoLevel ?? 1,
            };
            bakedTexture.SetPixels(outputPixels);
            bakedTexture.Apply();
        }
        catch (Exception exception)
        {
            if (bakedTexture != null)
            {
                UnityObject.DestroyImmediate(bakedTexture);
            }

            UnityEngine.Debug.LogWarning($"Could not bake lilToon metallic/smoothness map. {exception.Message}");
            return metallicGlossMap;
        }

        return CacheBakedTexture(bakedTexture, referenceTexture, ref AssetCache.MetallicGlossMap);
    }

    private static UnityColor[] ReadTexturePixels(UnityTexture texture, int width, int height, UnityColor fallback)
    {
        if (texture == null)
        {
            var fallbackPixels = new UnityColor[width * height];
            for (var i = 0; i < fallbackPixels.Length; i++)
            {
                fallbackPixels[i] = fallback;
            }

            return fallbackPixels;
        }

        UnityRenderTexture renderTexture = null;
        var currentRenderTexture = UnityRenderTexture.active;

        try
        {
            renderTexture = UnityRenderTexture.GetTemporary(width, height, 0, UnityRenderTextureFormat.Default, UnityRenderTextureReadWrite.Linear);
            Graphics.Blit(texture, renderTexture);
            UnityRenderTexture.active = renderTexture;

            var readableTexture = new UnityTexture2D(width, height, TextureFormat.RGBA32, false, true);
            readableTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readableTexture.Apply();
            var pixels = readableTexture.GetPixels();
            UnityObject.DestroyImmediate(readableTexture);
            return pixels;
        }
        finally
        {
            UnityRenderTexture.active = currentRenderTexture;
            if (renderTexture != null)
            {
                UnityRenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }

    private UnityTexture BakeEmissionMapWithLilToon(UnityTexture emissionMap, UnityTexture emissionBlendMask, UnityTexture albedoTexture)
    {
        var bakerShader = UnityShader.Find("Hidden/ltsother_baker");
        if (bakerShader == null)
        {
            UnityEngine.Debug.LogWarning("Could not find lilToon texture baker shader Hidden/ltsother_baker.");
            return null;
        }

        var sourceTexture2D = emissionMap as UnityTexture2D
            ?? emissionBlendMask as UnityTexture2D
            ?? albedoTexture as UnityTexture2D
            ?? UnityTexture2D.whiteTexture;
        UnityTexture2D bakedTexture = null;
        UnityMaterial bakerMaterial = null;

        try
        {
            bakerMaterial = new UnityMaterial(bakerShader);
            bakerMaterial.SetColor("_Color", UnityColor.white);
            bakerMaterial.SetTexture("_MainTex", emissionMap ?? UnityTexture2D.whiteTexture);
            bakerMaterial.SetTextureScale("_MainTex", GetTextureScale("_EmissionMap"));
            bakerMaterial.SetTextureOffset("_MainTex", GetTextureOffset("_EmissionMap"));
            bakerMaterial.SetVector("_MainTexHSVG", new Vector4(0, 1, 1, 1));
            bakerMaterial.SetFloat("_MainGradationStrength", 0);
            bakerMaterial.SetTexture("_MainGradationTex", UnityTexture2D.whiteTexture);
            bakerMaterial.SetTexture("_MainColorAdjustMask", UnityTexture2D.whiteTexture);

            bakerMaterial.SetFloat("_UseMain2ndTex", emissionBlendMask != null ? 1 : 0);
            bakerMaterial.SetColor("_Color2nd", UnityColor.white);
            bakerMaterial.SetTexture("_Main2ndTex", emissionBlendMask ?? UnityTexture2D.whiteTexture);
            bakerMaterial.SetTextureScale("_Main2ndTex", GetTextureScale("_EmissionBlendMask"));
            bakerMaterial.SetTextureOffset("_Main2ndTex", GetTextureOffset("_EmissionBlendMask"));
            bakerMaterial.SetFloat("_Main2ndTexAngle", 0);
            bakerMaterial.SetVector("_Main2ndTexDecalAnimation", new Vector4(1, 1, 1, 30));
            bakerMaterial.SetVector("_Main2ndTexDecalSubParam", new Vector4(1, 1, 0, 1));
            bakerMaterial.SetFloat("_Main2ndTexIsDecal", 0);
            bakerMaterial.SetFloat("_Main2ndTexIsLeftOnly", 0);
            bakerMaterial.SetFloat("_Main2ndTexIsRightOnly", 0);
            bakerMaterial.SetFloat("_Main2ndTexShouldCopy", 0);
            bakerMaterial.SetFloat("_Main2ndTexShouldFlipMirror", 0);
            bakerMaterial.SetFloat("_Main2ndTexShouldFlipCopy", 0);
            bakerMaterial.SetFloat("_Main2ndTexIsMSDF", 0);
            bakerMaterial.SetTexture("_Main2ndBlendMask", UnityTexture2D.whiteTexture);
            bakerMaterial.SetFloat("_Main2ndTexBlendMode", 3);

            var emissionMainStrength = Mathf.Clamp01(GetFloat("_EmissionMainStrength", 0));
            bakerMaterial.SetFloat("_UseMain3rdTex", emissionMainStrength > 0 && albedoTexture != null ? 1 : 0);
            bakerMaterial.SetColor("_Color3rd", new UnityColor(1, 1, 1, emissionMainStrength));
            bakerMaterial.SetTexture("_Main3rdTex", albedoTexture ?? UnityTexture2D.whiteTexture);
            bakerMaterial.SetTextureScale("_Main3rdTex", GetTextureScale("_MainTex"));
            bakerMaterial.SetTextureOffset("_Main3rdTex", GetTextureOffset("_MainTex"));
            bakerMaterial.SetFloat("_Main3rdTexAngle", 0);
            bakerMaterial.SetVector("_Main3rdTexDecalAnimation", new Vector4(1, 1, 1, 30));
            bakerMaterial.SetVector("_Main3rdTexDecalSubParam", new Vector4(1, 1, 0, 1));
            bakerMaterial.SetFloat("_Main3rdTexIsDecal", 0);
            bakerMaterial.SetFloat("_Main3rdTexIsLeftOnly", 0);
            bakerMaterial.SetFloat("_Main3rdTexIsRightOnly", 0);
            bakerMaterial.SetFloat("_Main3rdTexShouldCopy", 0);
            bakerMaterial.SetFloat("_Main3rdTexShouldFlipMirror", 0);
            bakerMaterial.SetFloat("_Main3rdTexShouldFlipCopy", 0);
            bakerMaterial.SetFloat("_Main3rdTexIsMSDF", 0);
            bakerMaterial.SetTexture("_Main3rdBlendMask", UnityTexture2D.whiteTexture);
            bakerMaterial.SetFloat("_Main3rdTexBlendMode", 3);

            bakedTexture = BakeTextureWithLilToon(sourceTexture2D, bakerMaterial, "LilToonConverter baked emission map");
        }
        catch (Exception exception)
        {
            if (bakedTexture != null)
            {
                UnityObject.DestroyImmediate(bakedTexture);
            }

            UnityEngine.Debug.LogWarning($"Could not bake lilToon emission map. {exception.Message}");
            return null;
        }
        finally
        {
            if (bakerMaterial != null)
            {
                UnityObject.DestroyImmediate(bakerMaterial);
            }
        }

        if (bakedTexture == null)
        {
            return null;
        }

        return CacheBakedTexture(bakedTexture, sourceTexture2D, ref AssetCache.EmissionMap);
    }

    private UnityTexture BakeShadowRampWithLilToon()
    {
        if (Material.HasProperty("_UseShadow") && Material.GetFloat("_UseShadow") == 0)
        {
            return null;
        }

        var bakerShader = UnityShader.Find("Hidden/ltsother_bakeramp");
        if (bakerShader == null)
        {
            UnityEngine.Debug.LogWarning("Could not find lilToon shadow ramp baker shader Hidden/ltsother_bakeramp.");
            return null;
        }

        UnityMaterial bakerMaterial = null;
        UnityTexture2D bakedRamp = null;
        UnityRenderTexture renderTexture = null;
        var currentRenderTexture = UnityRenderTexture.active;

        try
        {
            const int width = 256;
            const int height = 256;
            bakerMaterial = new UnityMaterial(bakerShader);
            CopyShadowRampBakerProperties(Material, bakerMaterial);

            renderTexture = UnityRenderTexture.GetTemporary(width, height, 0, UnityRenderTextureFormat.Default, UnityRenderTextureReadWrite.Default);
            UnityRenderTexture.active = renderTexture;
            Graphics.Blit(null, renderTexture, bakerMaterial);

            bakedRamp = new UnityTexture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "LilToonConverter baked shadow ramp",
                wrapMode = TextureWrapMode.Clamp,
            };
            bakedRamp.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            bakedRamp.Apply();
            ApplyShadowRampMaskGradient(bakedRamp);
        }
        catch (Exception exception)
        {
            if (bakedRamp != null)
            {
                UnityObject.DestroyImmediate(bakedRamp);
            }

            UnityEngine.Debug.LogWarning($"Could not bake lilToon shadow ramp. {exception.Message}");
            return null;
        }
        finally
        {
            UnityRenderTexture.active = currentRenderTexture;
            if (renderTexture != null)
            {
                UnityRenderTexture.ReleaseTemporary(renderTexture);
            }

            if (bakerMaterial != null)
            {
                UnityObject.DestroyImmediate(bakerMaterial);
            }
        }

        bakedRamp.wrapMode = TextureWrapMode.Clamp;
        return CacheBakedTexture(bakedRamp, null, ref AssetCache.ShadowRampTexture);
    }

    private static void CopyShadowRampBakerProperties(UnityMaterial source, UnityMaterial destination)
    {
        CopyFloat(source, destination, "_ShadowStrength");
        CopyFloat(source, destination, "_ShadowBorder");
        CopyFloat(source, destination, "_ShadowBlur");
        CopyFloat(source, destination, "_Shadow2ndBorder");
        CopyFloat(source, destination, "_Shadow2ndBlur");
        CopyFloat(source, destination, "_Shadow3rdBorder");
        CopyFloat(source, destination, "_Shadow3rdBlur");
        CopyFloat(source, destination, "_ShadowBorderRange");
        CopyColor(source, destination, "_ShadowColor");
        CopyColor(source, destination, "_Shadow2ndColor");
        CopyColor(source, destination, "_Shadow3rdColor");
        CopyColor(source, destination, "_ShadowBorderColor");
    }

    private static void CopyFloat(UnityMaterial source, UnityMaterial destination, string propertyName)
    {
        if (source.HasProperty(propertyName) && destination.HasProperty(propertyName))
        {
            destination.SetFloat(propertyName, source.GetFloat(propertyName));
        }
    }

    private static void CopyColor(UnityMaterial source, UnityMaterial destination, string propertyName)
    {
        if (source.HasProperty(propertyName) && destination.HasProperty(propertyName))
        {
            destination.SetColor(propertyName, source.GetColor(propertyName));
        }
    }

    private static void ApplyShadowRampMaskGradient(UnityTexture2D ramp)
    {
        var sourcePixels = ramp.GetPixels();
        var gradientPixels = new UnityColor[sourcePixels.Length];
        var width = ramp.width;
        var height = ramp.height;

        for (var y = 0; y < height; y++)
        {
            var mask = height > 1 ? y / (height - 1f) : 1f;
            for (var x = 0; x < width; x++)
            {
                var color = UnityColor.Lerp(UnityColor.white, sourcePixels[x], mask);
                color.a = 1;
                gradientPixels[x + y * width] = color;
            }
        }

        ramp.SetPixels(gradientPixels);
        ramp.Apply();
    }

    private static UnityTexture2D BakeTextureWithLilToon(UnityTexture2D sourceTexture, UnityMaterial material, string name)
    {
        var runBake = GetLilToonRunBakeMethod();
        if (runBake == null)
        {
            UnityEngine.Debug.LogWarning("Could not find lilToonInspector.RunBake; main texture was not baked.");
            return null;
        }

        object[] args = { null, sourceTexture, material, sourceTexture };
        runBake.Invoke(null, args);

        var bakedTexture = args[0] as UnityTexture2D;
        if (bakedTexture == null)
        {
            return null;
        }

        bakedTexture.name = name;
        bakedTexture.wrapMode = sourceTexture.wrapMode;
        bakedTexture.filterMode = sourceTexture.filterMode;
        bakedTexture.anisoLevel = sourceTexture.anisoLevel;
        return bakedTexture;
    }

    private UnityTexture2D CacheBakedTexture(UnityTexture2D bakedTexture, UnityTexture2D referenceTexture, ref UnityTexture2D cachedTexture)
    {
        if (referenceTexture != null)
        {
            bakedTexture.wrapMode = referenceTexture.wrapMode;
            bakedTexture.filterMode = referenceTexture.filterMode;
            bakedTexture.anisoLevel = referenceTexture.anisoLevel;
        }

        if (cachedTexture != null && cachedTexture != bakedTexture && !EditorUtility.IsPersistent(cachedTexture))
        {
            UnityObject.DestroyImmediate(cachedTexture);
        }

        cachedTexture = bakedTexture;
        return bakedTexture;
    }

    private static MethodInfo GetLilToonRunBakeMethod()
    {
        var inspectorType = GetLilToonEditorType("lilToon.lilToonInspector");
        return inspectorType?.GetMethod(
            "RunBake",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(UnityTexture2D).MakeByRefType(), typeof(UnityTexture2D), typeof(UnityMaterial), typeof(UnityTexture2D) },
            null);
    }

    private static Type GetLilToonEditorType(string typeName)
    {
        var type = Type.GetType($"{typeName}, lilToon.Editor");
        if (type != null)
        {
            return type;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void SetFallbackTexture(UnityMaterial material, string property, UnityTexture fallback)
    {
        if (material.HasProperty(property) && material.GetTexture(property) == null)
        {
            material.SetTexture(property, fallback);
        }
    }

    private void SetColorFromSource(UnityMaterial material, string property, UnityColor fallback)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, GetColor(property, fallback));
        }
    }

    private Vector2 GetXiexeRimShape(string borderProperty, string blurProperty, string fresnelPowerProperty)
    {
        var border = GetFloat(borderProperty, 0.5f);
        var blur = GetFloat(blurProperty, 0.1f);
        var fresnelPower = Math.Max(GetFloat(fresnelPowerProperty, 1), 0.01f);
        var borderMin = Mathf.Clamp01(border - blur * 0.5f);
        var borderMax = Mathf.Clamp01(border + blur * 0.5f);
        var xiexeMin = Mathf.Pow(borderMin, 1 / fresnelPower);
        var xiexeMax = Mathf.Pow(borderMax, 1 / fresnelPower);

        return new Vector2(
            Mathf.Clamp01((xiexeMin + xiexeMax) * 0.5f),
            Mathf.Clamp01((xiexeMax - xiexeMin) * 0.5f));
    }

    private float GetFloat(string property, float defaultValue)
    {
        return Material.HasProperty(property) ? Material.GetFloat(property) : defaultValue;
    }

    private Vector4 GetVector(string property, Vector4 defaultValue)
    {
        return Material.HasProperty(property) ? Material.GetVector(property) : defaultValue;
    }

    private UnityColor GetColor(string property, UnityColor defaultValue)
    {
        return Material.HasProperty(property) ? Material.GetColor(property) : defaultValue;
    }

    private UnityTexture GetTexture(string property)
    {
        return Material.HasProperty(property) ? Material.GetTexture(property) : null;
    }

    private Vector2 GetTextureOffset(string property)
    {
        return Material.HasProperty(property) ? Material.GetTextureOffset(property) : Vector2.zero;
    }

    private Vector2 GetTextureScale(string property)
    {
        return Material.HasProperty(property) ? Material.GetTextureScale(property) : Vector2.one;
    }
}
