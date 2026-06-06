using FrooxEngine;
using UnityEngine;

// lilToon FakeShadow is a projected/offset unlit helper rather than a surface
// material. Do not approximate it as XiexeToon, because that produces a
// visibly different colored overlay.
[MaterialConverter(false, "_lil/[Optional] lilToonFakeShadow")]
public class LilToonFakeShadowConverter : ResoniteMaterialConverter
{
    private UnlitMaterialWrapper Unlit;

    public override IAssetProvider<FrooxEngine.Material> UpdateConversion(
        UnityEngine.Material material,
        IConversionContext context)
    {
        if (Unlit == null)
        {
            Unlit = gameObject.AddComponent<UnlitMaterialWrapper>();
        }

        var data = Unlit.Data;
        data.TintColor = UnityEngine.Color.clear.ToColorX_sRGB();
        data.BlendMode = BlendMode.Cutout;
        data.AlphaCutoff = 1;
        data.Sidedness = Sidedness.Front;
        data.Texture = null;

        return data;
    }
}
