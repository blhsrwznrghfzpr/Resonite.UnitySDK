using FrooxEngine;
using UnityEditor;
using UnityEngine;

// This converter is a prototype meant to convert materials using
// the Poiyomi Toon family of shaders into a Resonite PBS Material.
//
// Poiyomi Toon shaders are freely available under the MIT license.
// Documentation and download links are available at https://www.poiyomi.com/
//
// However, this converter only uses the material's named properties.
// So neither the Poiyomi Toon shaders nor its source code
// need to be available in the Unity project for the converter to work.

public class PoiyomiPbsConverter
{
    private PBS_Metallic Pbs;

    private UnityEngine.Material Material;
    private IConversionContext Context;
    private PoiyomiAssetCache AssetCache;

    public PoiyomiPbsConverter(PBS_Metallic Pbs, UnityEngine.Material Material, IConversionContext Context, PoiyomiAssetCache AssetCache)
    {
        this.Pbs = Pbs;
        this.Material = Material;
        this.Context = Context;
        this.AssetCache = AssetCache;
    }

    public IAssetProvider<FrooxEngine.Material> UpdateConversion()
    {
        UpdateRenderingSettings();
        UpdateMainTexture();
        UpdateEmission();
        UpdateNormal();
        UpdateHeight();
        UpdateOcclusion();
        UpdateDetail();
        UpdateMetallicSmoothness();
        return Pbs;
    }

    private void UpdateRenderingSettings()
    {
        Pbs.BlendMode = PoiyomiBlendModeComputer.FromPoiyomi(Material);
        Pbs.AlphaCutoff = Material.GetFloat("_Cutoff");
        Pbs.OffsetFactor = Material.GetFloat("_OffsetFactor");
        Pbs.OffsetUnits = Material.GetFloat("_OffsetUnits");
        Pbs.RenderQueue = Material.renderQueue;
    }

    private void UpdateMainTexture()
    {
        Pbs.AlbedoColor = Material.color.ToColorX_sRGB();
        Pbs.AlbedoTexture = Context.GetITexture2D(Material.mainTexture);
        Pbs.TextureOffset = Material.mainTextureOffset;
        Pbs.TextureScale = Material.mainTextureScale;
    }

    private void UpdateEmission()
    {
        if (Material.GetFloat("_EnableEmission") > 0)
        {
            Color emissiveColor = Material.GetColor("_EmissionColor");
            emissiveColor *= Material.GetFloat("_EmissionStrength");
            Pbs.EmissiveColor = emissiveColor.ToColorX_Auto();
            Pbs.EmissiveMap = Context.GetITexture2D(Material.GetTexture("_EmissionMap"));
            return;
        }
        if (Pbs.EmissiveMap != null)
        {
            Pbs.EmissiveMap = null;
            Pbs.EmissiveColor = Color.black.ToColorX_sRGB();
        }
    }

    private void UpdateNormal()
    {
        Pbs.NormalMap = Context.GetITexture2D(Material.GetTexture("_BumpMap"));
        Pbs.NormalScale = Material.GetFloat("_BumpScale");
    }

    private void UpdateHeight()
    {
        if (Material.GetFloat("_VertexManipulationsEnabled") == 0)
        {
            if (Pbs.HeightMap != null)
            {
                Pbs.HeightMap = null;
                Pbs.HeightScale = 0;
            }
            return;
        }

        Pbs.HeightMap = Context.GetITexture2D(Material.GetTexture("_VertexManipulationHeightMask"));
        Pbs.HeightScale = -Material.GetFloat("_VertexManipulationHeight");
    }

    private void UpdateOcclusion()
    {
        Pbs.OcclusionMap = Context.GetITexture2D(Material.GetTexture("_LightingAOMaps"));
    }

    private void UpdateDetail()
    {
        if (Material.GetFloat("_DetailEnabled") == 0)
        {
            Pbs.DetailAlbedoTexture = null;
            Pbs.DetailNormalMap = null;
            return;
        }
        Pbs.DetailAlbedoTexture = Context.GetITexture2D(Material.GetTexture("_DetailTex"));
        Pbs.DetailTextureOffset = Material.GetTextureOffset("_DetailTex");
        Pbs.DetailTextureScale = Material.GetTextureScale("_DetailTex");
        Pbs.DetailNormalMap = Context.GetITexture2D(Material.GetTexture("_DetailNormalMap"));
        Pbs.DetailNormalScale = Material.GetFloat("_DetailNormalMapScale");
    }

    private void UpdateMetallicSmoothness()
    {
        if (Material.GetFloat("_MochieBRDF") > 0)
        {
            Pbs.MetallicMap = Context.GetITexture2D(MetallicSwizzle());
            Pbs.Metallic = Material.GetFloat("_MochieMetallicMultiplier");
            Pbs.Smoothness = Material.GetFloat("_MochieRoughnessMultiplier");
            return;
        }

        if (Pbs.MetallicMap != null)
        {
            Pbs.MetallicMap = null;
            Pbs.Metallic = 0;
            Pbs.Smoothness = 0;
        }
    }

    private Texture MetallicSwizzle()
    {
        PoiyomiColorChannel metallic = (PoiyomiColorChannel)Material.GetFloat("_MochieMetallicMapsMetallicChannel");
        PoiyomiColorChannel smoothness = (PoiyomiColorChannel)Material.GetFloat("_MochieMetallicMapsRoughnessChannel");
        bool invertMetallic = Material.GetFloat("_MochieMetallicMapInvert") > 0;
        bool invertSmoothness = Material.GetFloat("_MochieRoughnessMapInvert") > 0;

        Texture originalMetallicRaw = Material.GetTexture("_MochieMetallicMaps");
        if (
            metallic == PoiyomiColorChannel.R && !invertMetallic &&
            smoothness == PoiyomiColorChannel.A && !invertSmoothness
        )
        {
            // Standard mapping, no swizzle necessary
            return originalMetallicRaw;
        }

        if (originalMetallicRaw is not UnityEngine.Texture2D originalMetallic)
        {
            Debug.LogWarning($"Packed metallic map {originalMetallicRaw.name} is not 2D; could not perform channel swizzling. You will need to unpack and repack the channels manually in Resonite.");
            return originalMetallicRaw;
        }

        TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(originalMetallic)) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Packed metallic map {originalMetallic.name} is not importable; could not perform channel swizzling. You will need to unpack and repack the channels manually in Resonite.");
            return originalMetallic;
        }

        Debug.Log($"Packed metallic map {originalMetallic.name} uses non-standard channel packing. Temporarily re-importing it with swizzled channels to match the format expected by Resonite.");

        // Save original texture importer settings to use as a backup for later
        TextureImporterSettings originalSettings = new();
        importer.ReadTextureSettings(originalSettings);
        // Create a copy of the importer settings, applying the swizzle specified by the Poiyomi material settings
        TextureImporterSettings copySettings = new();
        originalSettings.CopyTo(copySettings);
        copySettings.swizzleR = PoiyomiColorChannelMethods.SwizzleFromChannel(originalSettings, metallic, invertMetallic);
        copySettings.swizzleG = TextureImporterSwizzle.Zero;
        copySettings.swizzleB = TextureImporterSwizzle.Zero;
        copySettings.swizzleA = PoiyomiColorChannelMethods.SwizzleFromChannel(originalSettings, smoothness, invertSmoothness);
        copySettings.readable = true;
        // Reimport the texture with the applied swizzle
        importer.SetTextureSettings(copySettings);
        importer.SaveAndReimport();

        // Setup a metallic texture in the asset cache to store the result of the swizzle
        var metallicTexture = AssetCache.MetallicTexture;
        if (metallicTexture == null || metallicTexture.width != originalMetallic.width || metallicTexture.height != originalMetallic.height)
        {
            if (metallicTexture != null)
            {
                UnityEngine.Texture2D.Destroy(metallicTexture);
            }
            metallicTexture = new(originalMetallic.width, originalMetallic.height, originalMetallic.format, false, true, true)
            {
                name = "PoiyomiConverter swizzled metallic texture"
            };
            AssetCache.MetallicTexture = metallicTexture;
        }

        // Make a pixel-wise copy of the swizzled texture
        Graphics.CopyTexture(originalMetallic, 0, 0, metallicTexture, 0, 0);

        // Restore the original backed-up settings of the importer, to restore it to its original state in Unity
        importer.SetTextureSettings(originalSettings);
        importer.SaveAndReimport();

        return metallicTexture;
    }
}
