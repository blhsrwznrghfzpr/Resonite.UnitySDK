using FrooxEngine;
using UnityEngine;

// Converts lilToon-family materials into Resonite's XiexeToonMaterial.
// The converter reads only named material properties, so lilToon does not
// need to be installed in the Unity project doing the export.
[MaterialConverter(true,
    "lilToon",
    "_lil/[Optional] lilToonOverlay",
    "_lil/[Optional] lilToonOutlineOnly",
    "_lil/[Optional] lilToonFurOnly",
    "_lil/lilToonMulti",
    "Hidden/lilToonLite",
    "Hidden/lilToonLiteCutout",
    "Hidden/lilToonLiteTransparent")]
public class LilToonConverter : ResoniteMaterialConverter
{
    private XiexeToonMaterialWrapper XiexeComponent;
    private readonly LilToonAssetCache AssetCache = new();

    public static float? EvaluateHeuristicConversion(UnityEngine.Material material)
    {
        if (material.shader == null)
        {
            return null;
        }

        if (material.shader.name.Contains("lilToon"))
        {
            return 100;
        }

        if (!material.HasProperty("_lilToonVersion"))
        {
            return null;
        }

        return PropertyBasedCompatibilityEvaluator.ComputeCompatibility(material, new[]
        {
            "_MainTex",
            "_Main2ndTex",
            "_Main3rdTex",
            "_AlphaMask",
            "_BumpMap",
            "_MetallicGlossMap",
            "_EmissionMap",
            "_MatCapTex",
            "_OutlineWidthMask",
            "_Ramp",
            "_ShadowBorderMask",
        });
    }

    public override IAssetProvider<FrooxEngine.Material> UpdateConversion(UnityEngine.Material material, IConversionContext context)
    {
        if (XiexeComponent == null)
        {
            XiexeComponent = gameObject.AddComponent<XiexeToonMaterialWrapper>();
        }

        return new LilToonXiexeConverter(XiexeComponent.Data, material, context, AssetCache).UpdateConversion();
    }
}
