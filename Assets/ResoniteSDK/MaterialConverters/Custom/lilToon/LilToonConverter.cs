using FrooxEngine;
using UnityEngine;

// lilToon shaders are freely available under the MIT license.
// Documentation and download links are available at https://lilxyzw.github.io/lilToon/
[MaterialConverter(true, "lilToon")]
public class LilToonConverter : ResoniteMaterialConverter
{
    private XiexeToonMaterialWrapper XiexeComponent;
    private readonly LilToonAssetCache AssetCache = new();

    public static float? EvaluateHeuristicConversion(UnityEngine.Material material)
    {
        if (!material.shader.name.Contains("lilToon"))
        {
            return null;
        }
        return 0;
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
