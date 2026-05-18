using System;
using System.IO;
using System.Reflection;
using FrooxEngine;
using UnityEditor;
using UnityEngine;
using UnityColor = UnityEngine.Color;
using UnityMaterial = UnityEngine.Material;
using UnityObject = UnityEngine.Object;
using UnityShader = UnityEngine.Shader;
using UnityTexture = UnityEngine.Texture;
using UnityTexture2D = UnityEngine.Texture2D;

public class LilToonXiexeConverter
{
    private const string BakeAssetFolder = "Assets/ResoniteSDK/Generated/LilToon";

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
        Xiexe.ShadowRampMask = Context.GetITexture2D(GetTexture("_ShadowBorderMask"));
        Xiexe.ShadowRampMaskOffset = GetTextureOffset("_ShadowBorderMask");
        Xiexe.ShadowRampMaskScale = GetTextureScale("_ShadowBorderMask");
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

        return CacheBakedTexture(bakedTexture, "main", sourceTexture2D, false, ref AssetCache.MainTexture);
    }

    private UnityTexture BakeShadowRampWithLilToon()
    {
        if (Material.HasProperty("_UseShadow") && Material.GetFloat("_UseShadow") == 0)
        {
            return null;
        }

        var convert = GetLilToonRampConvertMethod();
        if (convert == null)
        {
            return null;
        }

        UnityTexture2D bakedRamp;
        try
        {
            bakedRamp = convert.Invoke(null, new object[] { Material, 128 }) as UnityTexture2D;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"Could not bake lilToon shadow ramp through lilToon2Ramp.Convert. {exception.Message}");
            return null;
        }

        if (bakedRamp == null)
        {
            return null;
        }

        bakedRamp.name = "LilToonConverter baked shadow ramp";
        bakedRamp.wrapMode = TextureWrapMode.Clamp;
        return CacheBakedTexture(bakedRamp, "shadow-ramp", null, true, ref AssetCache.ShadowRampTexture);
    }

    private static MethodInfo GetLilToonRampConvertMethod()
    {
        var rampBakerType = GetLilToonEditorType("lilToon.lilToon2Ramp");
        return rampBakerType?.GetMethod("Convert", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(UnityMaterial), typeof(int) }, null);
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

    private UnityTexture2D CacheBakedTexture(UnityTexture2D bakedTexture, string suffix, UnityTexture2D referenceTexture, bool isDataTexture, ref UnityTexture2D cachedTexture)
    {
        var wrapMode = referenceTexture != null ? referenceTexture.wrapMode : bakedTexture.wrapMode;
        var filterMode = referenceTexture != null ? referenceTexture.filterMode : bakedTexture.filterMode;
        var anisoLevel = referenceTexture != null ? referenceTexture.anisoLevel : bakedTexture.anisoLevel;
        var mipmapEnabled = referenceTexture != null && referenceTexture.mipmapCount > 1;

        try
        {
            Directory.CreateDirectory(BakeAssetFolder);

            var assetPath = GetBakeAssetPath(suffix);
            File.WriteAllBytes(assetPath, bakedTexture.EncodeToPNG());

            AssetDatabase.ImportAsset(assetPath);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = wrapMode;
                importer.filterMode = filterMode;
                importer.anisoLevel = anisoLevel;
                importer.mipmapEnabled = mipmapEnabled;
                importer.sRGBTexture = !isDataTexture;
                importer.SaveAndReimport();
            }

            cachedTexture = AssetDatabase.LoadAssetAtPath<UnityTexture2D>(assetPath);
            if (cachedTexture == null)
            {
                UnityEngine.Debug.LogWarning($"Could not load cached lilToon baked texture at {assetPath}.");
                cachedTexture = bakedTexture;
                return bakedTexture;
            }

            UnityObject.DestroyImmediate(bakedTexture);
            return cachedTexture;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"Could not cache lilToon baked texture as an asset. Falling back to in-memory texture. {exception.Message}");
            cachedTexture = bakedTexture;
            return bakedTexture;
        }
    }

    private string GetBakeAssetPath(string suffix)
    {
        var materialAssetPath = AssetDatabase.GetAssetPath(Material);
        var materialKey = string.IsNullOrEmpty(materialAssetPath)
            ? Material.GetInstanceID().ToString()
            : AssetDatabase.AssetPathToGUID(materialAssetPath);

        return $"{BakeAssetFolder}/{SanitizeFileName(Material.name)}_{materialKey}_{suffix}.png";
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "lilToon" : fileName;
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
