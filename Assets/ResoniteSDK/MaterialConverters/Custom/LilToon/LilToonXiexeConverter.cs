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
        Xiexe.MainTexture = Context.GetITexture2D(BakeMainTextureWithLilToon() ?? GetTexture("_MainTex"));
        Xiexe.Color = Color.white.ToColorX_sRGB();
        Xiexe.MainTextureScale = GetTextureScale("_MainTex");
        Xiexe.MainTextureOffset = GetTextureOffset("_MainTex");
        Xiexe.Saturation = 1;
        Xiexe.NormalMap = Context.GetITexture2D(GetTexture("_BumpMap"));
        Xiexe.NormalMapScale = GetTextureScale("_BumpMap");
        Xiexe.NormalMapOffset = GetTextureOffset("_BumpMap");
        Xiexe.MetallicGlossMap = Context.GetITexture2D(GetTexture("_MetallicGlossMap"));
        Xiexe.MetallicGlossMapScale = GetTextureScale("_MetallicGlossMap");
        Xiexe.MetallicGlossMapOffset = GetTextureOffset("_MetallicGlossMap");
        Xiexe.EmissionMap = Context.GetITexture2D(GetTexture("_EmissionMap"));
        Xiexe.EmissionMapScale = GetTextureScale("_EmissionMap");
        Xiexe.EmissionMapOffset = GetTextureOffset("_EmissionMap");
        Xiexe.RimColor = Color.white.ToColorX_sRGB();
        Xiexe.RimAlbedoTint = 0;
        Xiexe.RimAttenuationEffect = 1;
        Xiexe.RimIntensity = 0;
        Xiexe.RimRange = 0.7f;
        Xiexe.RimThreshold = 0.1f;
        Xiexe.RimSharpness = 0.1f;
        Xiexe.Matcap = Context.GetITexture2D(GetTexture("_MatCapTex"));
        Xiexe.OcclusionMap = Context.GetITexture2D(GetTexture("_ShadowBorderMask"));
        Xiexe.OcclusionMapScale = GetTextureScale("_ShadowBorderMask");
        Xiexe.OcclusionMapOffset = GetTextureOffset("_ShadowBorderMask");
        Xiexe.OutlineMask = Context.GetITexture2D(GetTexture("_OutlineWidthMask"));
        Xiexe.ShadowRamp = Context.GetITexture2D(GetTexture("_Ramp") ?? BakeShadowRampWithLilToon());
        // XiexeToon's null ShadowRampMask behaves differently from lilToon's null
        // _ShadowStrengthMask, which means no shadow strength mask. Preserve that with white.
        Xiexe.ShadowRampMask = Context.GetITexture2D(GetTexture("_ShadowStrengthMask") ?? UnityTexture2D.whiteTexture);
        Xiexe.ShadowRampMaskScale = GetTextureScale("_ShadowStrengthMask");
        Xiexe.ShadowRampMaskOffset = GetTextureOffset("_ShadowStrengthMask");
        Xiexe.ShadowRim = Color.white.ToColorX_sRGB();
        Xiexe.ShadowSharpness = 0.5f;
        Xiexe.ShadowRimRange = 0.7f;
        Xiexe.ShadowRimThreshold = 0.1f;
        Xiexe.ShadowRimSharpness = 0.3f;
        Xiexe.ShadowRimAlbedoTint = 0;
        Xiexe.ColorMask = ColorMask.RGBA;

        return Xiexe;
    }

    private UnityTexture BakeMainTextureWithLilToon()
    {
        var shouldBakeMain = HasNonDefaultColor()
            || GetVector("_MainTexHSVG", new Vector4(0, 1, 1, 1)) != new Vector4(0, 1, 1, 1)
            || GetFloat("_MainGradationStrength", 0) != 0
            || GetFloat("_UseMain2ndTex", 0) != 0
            || GetFloat("_UseMain3rdTex", 0) != 0;
        var shouldBakeAlpha = GetFloat("_AlphaMaskMode", 0) != 0 && GetTexture("_AlphaMask") != null;

        if (!shouldBakeMain && !shouldBakeAlpha)
        {
            return null;
        }

        var bakerShader = UnityShader.Find("Hidden/ltsother_baker");
        if (bakerShader == null)
        {
            UnityEngine.Debug.LogWarning("Could not find lilToon main texture baker shader Hidden/ltsother_baker.");
            return null;
        }

        var sourceTexture = GetTexture("_MainTex") ?? UnityTexture2D.whiteTexture;
        var sourceTexture2D = sourceTexture as UnityTexture2D ?? UnityTexture2D.whiteTexture;

        UnityTexture2D bakedTexture = null;
        UnityMaterial bakerMaterial = null;

        try
        {
            bakerMaterial = new UnityMaterial(bakerShader);
            bakerMaterial.CopyPropertiesFromMaterial(Material);
            SetFallbackTexture(bakerMaterial, "_MainTex", sourceTexture);
            SetFallbackTexture(bakerMaterial, "_MainGradationTex", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_MainColorAdjustMask", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_Main2ndTex", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_Main2ndBlendMask", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_Main3rdTex", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_Main3rdBlendMask", UnityTexture2D.whiteTexture);
            SetFallbackTexture(bakerMaterial, "_AlphaMask", UnityTexture2D.whiteTexture);

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

        return CacheBakedTexture(bakedTexture, sourceTexture2D, ref AssetCache.MainTexture);
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
            bakerMaterial = new UnityMaterial(Material)
            {
                shader = bakerShader,
            };

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
                gradientPixels[x + y * width] = UnityColor.Lerp(UnityColor.white, sourcePixels[x], mask);
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

    private bool HasNonDefaultColor()
    {
        return Material.HasProperty("_Color") && Material.GetColor("_Color") != UnityColor.white;
    }

    private float GetFloat(string property, float defaultValue)
    {
        return Material.HasProperty(property) ? Material.GetFloat(property) : defaultValue;
    }

    private Vector4 GetVector(string property, Vector4 defaultValue)
    {
        return Material.HasProperty(property) ? Material.GetVector(property) : defaultValue;
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
