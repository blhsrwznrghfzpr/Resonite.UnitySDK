using FrooxEngine;
using UnityEngine;

public class LilToonXiexeConverter
{
    private readonly XiexeToonMaterial Xiexe;
    private readonly UnityEngine.Material Material;
    private readonly IConversionContext Context;
    private readonly LilToonAssetCache AssetCache;

    public LilToonXiexeConverter(XiexeToonMaterial Xiexe, UnityEngine.Material Material, IConversionContext Context, LilToonAssetCache AssetCache)
    {
        this.Xiexe = Xiexe;
        this.Material = Material;
        this.Context = Context;
        this.AssetCache = AssetCache;
    }

    public IAssetProvider<FrooxEngine.Material> UpdateConversion()
    {
        Xiexe.MainTexture = Context.GetITexture2D(Material.mainTexture);
        Xiexe.MainTextureScale = Material.mainTextureScale;
        Xiexe.MainTextureOffset = Material.mainTextureOffset;
        Xiexe.Color = Material.GetColor("_Color").ToColorX_sRGB();
        Xiexe.Saturation = 1;
        Xiexe.ShadowRim = Color.white.ToColorX_sRGB();
        Xiexe.ColorMask = (ColorMask)Material.GetFloat("_ColorMask");
        Xiexe.RenderQueue = Material.renderQueue;

        return Xiexe;
    }

}
