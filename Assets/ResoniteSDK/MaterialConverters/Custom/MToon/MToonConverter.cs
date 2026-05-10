using FrooxEngine;
using UnityEngine;

// This converter maps UniVRM MToon materials to Resonite's XiexeToonMaterial.
//
// It only reads Unity material properties by name. UniVRM does not need to be
// referenced by this SDK project for the converter to compile.
[MaterialConverter(false, "VRM/MToon")]
public class MToonConverter : ResoniteMaterialConverter
{
    private FrooxEngine.XiexeToonMaterialWrapper XiexeComponent;

    private FrooxEngine.Panner2DWrapper MainTexturePanner;
    private FrooxEngine.Panner2DWrapper NormalMapPanner;
    private FrooxEngine.Panner2DWrapper EmissionMapPanner;
    private FrooxEngine.Panner2DWrapper OcclusionMapPanner;
    private FrooxEngine.Panner2DWrapper ShadowRampMaskPanner;

    public override IAssetProvider<FrooxEngine.Material> UpdateConversion(UnityEngine.Material material, IConversionContext context)
    {
        if (XiexeComponent == null)
        {
            XiexeComponent = gameObject.AddComponent<FrooxEngine.XiexeToonMaterialWrapper>();
        }

        var converted = new MToonXiexeConverter(XiexeComponent.Data, material, context).UpdateConversion();
        UpdateUvScrollPanners(material, XiexeComponent.Data);
        return converted;
    }

    private void UpdateUvScrollPanners(UnityEngine.Material material, FrooxEngine.XiexeToonMaterial xiexe)
    {
        var speed = new Vector2(
            material.GetFloat("_UvAnimScrollX"),
            material.GetFloat("_UvAnimScrollY"));
        var hasMask = material.GetTexture("_UvAnimMaskTexture") != null;
        var hasRotation = !Mathf.Approximately(material.GetFloat("_UvAnimRotation"), 0);
        var hasScroll = !Mathf.Approximately(speed.sqrMagnitude, 0);
        var canConvertScroll = hasScroll && !hasMask && !hasRotation;
        if (!canConvertScroll)
        {
            return;
        }

        if (xiexe.MainTexture != null)
        {
            UpdatePanner(ref MainTexturePanner, xiexe.MainTextureOffset_Element.Member, speed, xiexe.MainTextureOffset);
        }
        if (xiexe.NormalMap != null)
        {
            UpdatePanner(ref NormalMapPanner, xiexe.NormalMapOffset_Element.Member, speed, xiexe.NormalMapOffset);
        }
        if (xiexe.EmissionMap != null)
        {
            UpdatePanner(ref EmissionMapPanner, xiexe.EmissionMapOffset_Element.Member, speed, xiexe.EmissionMapOffset);
        }
        if (xiexe.OcclusionMap != null)
        {
            UpdatePanner(ref OcclusionMapPanner, xiexe.OcclusionMapOffset_Element.Member, speed, xiexe.OcclusionMapOffset);
        }
        if (xiexe.ShadowRampMask != null)
        {
            UpdatePanner(ref ShadowRampMaskPanner, xiexe.ShadowRampMaskOffset_Element.Member, speed, xiexe.ShadowRampMaskOffset);
        }
    }

    private void UpdatePanner(
        ref FrooxEngine.Panner2DWrapper panner,
        FrooxEngine.IField<Vector2> target,
        Vector2 speed,
        Vector2 offset)
    {
        if (panner == null)
        {
            panner = gameObject.AddComponent<FrooxEngine.Panner2DWrapper>();
        }

        var data = panner.Data;
        data.Enabled = true;
        data.persistent = true;
        data._target = target;
        data._speed = speed;
        data._offset = offset;
        data._repeat = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
    }

}
