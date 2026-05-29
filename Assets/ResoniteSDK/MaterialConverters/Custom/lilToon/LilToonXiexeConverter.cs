using System;
using FrooxEngine;
using UnityEngine;
using UnityEditor;

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
        UpdateShadowRamp();

        return Xiexe;
    }

    private void UpdateShadowRamp()
    {
        if (Material.GetFloat("_UseShadow") == 0)
        {
            return;
        }

        var bakerShader = UnityEngine.Shader.Find("Hidden/ltsother_bakeramp");
        if (bakerShader == null)
        {
            UnityEngine.Debug.LogWarning("Could not find lilToon shadow ramp baker shader Hidden/ltsother_bakeramp.");
            return;
        }

        UnityEngine.Material bakerMaterial = null;
        UnityEngine.Texture2D bakedRamp = null;
        UnityEngine.RenderTexture renderTexture = null;
        var currentRenderTexture = UnityEngine.RenderTexture.active;

        try
        {
            const int width = 256;
            const int height = 256;
            const int sourceHeight = 1;
            // Avoid assigning source.shader on Material Variants. Copy the saved values
            // into a normal baker material that already uses Hidden/ltsother_bakeramp.
            bakerMaterial = new UnityEngine.Material(bakerShader);
            bakerMaterial.CopyPropertiesFromMaterial(Material);

            // Bake lilToon's horizontal shadow ramp into the first row.
            renderTexture = UnityEngine.RenderTexture.GetTemporary(width, sourceHeight, 0, UnityEngine.RenderTextureFormat.Default, UnityEngine.RenderTextureReadWrite.Default);
            UnityEngine.RenderTexture.active = renderTexture;
            Graphics.Blit(null, renderTexture, bakerMaterial);

            bakedRamp = new UnityEngine.Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "LilToonConverter baked shadow ramp",
                wrapMode = TextureWrapMode.Clamp,
            };
            bakedRamp.ReadPixels(new Rect(0, 0, width, sourceHeight), 0, 0);

            // Expand the horizontal ramp vertically so Xiexe's ShadowRampMask can sample it.
            var sourcePixels = bakedRamp.GetPixels(0, 0, width, 1);
            var gradientPixels = new Color[width * height];

            for (var y = 0; y < height; y++)
            {
                var mask = height > 1 ? y / (height - 1f) : 1f;
                for (var x = 0; x < width; x++)
                {
                    var color = Color.Lerp(Color.white, sourcePixels[x], mask);
                    gradientPixels[x + y * width] = color;
                }
            }

            bakedRamp.SetPixels(gradientPixels);
            bakedRamp.Apply();
        }
        catch (Exception exception)
        {
            if (bakedRamp != null)
            {
                UnityEngine.Object.DestroyImmediate(bakedRamp);
            }

            UnityEngine.Debug.LogWarning($"Could not bake lilToon shadow ramp. {exception.Message}");
            return;
        }
        finally
        {
            UnityEngine.RenderTexture.active = currentRenderTexture;
            if (renderTexture != null)
            {
                UnityEngine.RenderTexture.ReleaseTemporary(renderTexture);
            }

            if (bakerMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(bakerMaterial);
            }
        }

        if (AssetCache.ShadowRampTexture != null && AssetCache.ShadowRampTexture != bakedRamp && !EditorUtility.IsPersistent(AssetCache.ShadowRampTexture))
        {
            UnityEngine.Object.DestroyImmediate(AssetCache.ShadowRampTexture);
        }

        AssetCache.ShadowRampTexture = bakedRamp;

        Xiexe.ShadowRamp = Context.GetITexture2D(bakedRamp);
        // XiexeToon's null ShadowRampMask behaves differently from lilToon's null
        // _ShadowStrengthMask, which means no shadow strength mask. Preserve that with white.
        if (Material.GetTexture("_ShadowStrengthMask") == null)
        {
            Xiexe.ShadowRampMask = Context.GetITexture2D(UnityEngine.Texture2D.whiteTexture);
            Xiexe.ShadowRampMaskScale = Vector2.one;
            Xiexe.ShadowRampMaskOffset = Vector2.zero;
            return;
        }
        else
        {
            Xiexe.ShadowRampMask = Context.GetITexture2D(Material.GetTexture("_ShadowStrengthMask"));
            Xiexe.ShadowRampMaskScale = Material.GetTextureScale("_ShadowStrengthMask");
            Xiexe.ShadowRampMaskOffset = Material.GetTextureOffset("_ShadowStrengthMask");
        }
    }

}
