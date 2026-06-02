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
        var mainTexture = Material.mainTexture;
        var mainTextureScale = Material.mainTextureScale;
        var mainTextureOffset = Material.mainTextureOffset;
        Xiexe.MainTexture = Context.GetITexture2D(mainTexture);
        Xiexe.MainTextureScale = mainTextureScale;
        Xiexe.MainTextureOffset = mainTextureOffset;
        Xiexe.Color = Material.GetColor("_Color").ToColorX_sRGB();
        Xiexe.Saturation = 1;
        Xiexe.ShadowRim = Color.white.ToColorX_sRGB();
        Xiexe.ColorMask = (ColorMask)Material.GetFloat("_ColorMask");
        Xiexe.RenderQueue = Material.renderQueue;
        UpdateEmission(mainTexture, mainTextureScale, mainTextureOffset);
        UpdateOcclusion();
        UpdateOutline();
        UpdateShadowRamp();

        return Xiexe;
    }

    private void UpdateEmission(Texture mainTexture, Vector2 mainTextureScale, Vector2 mainTextureOffset)
    {
        if (Material.GetFloat("_UseEmission") == 0)
        {
            Xiexe.EmissionMap = null;
            return;
        }

        var emissionColor = Material.GetColor("_EmissionColor");
        var emissionFluorescence = Mathf.Clamp01(Material.GetFloat("_EmissionFluorescence"));
        if (emissionFluorescence != 0)
        {
            // lilToon multiplies fluorescence emission by a lighting-dependent
            // invLighting value. Xiexe emission is static, so use the maximum-ish
            // value of lilToon's curve as a conservative approximation.
            var fluorescenceScale = Mathf.Lerp(1, 0.375f, emissionFluorescence);
            emissionColor.r *= fluorescenceScale;
            emissionColor.g *= fluorescenceScale;
            emissionColor.b *= fluorescenceScale;
        }
        Xiexe.EmissionColor = emissionColor.ToColorX_sRGB();

        var emissionMap = Material.GetTexture("_EmissionMap");
        var emissionBlendMask = Material.GetTexture("_EmissionBlendMask");
        var emissionMapUV = (int)Material.GetFloat("_EmissionMap_UVMode");
        var emissionMainStrength = Material.GetFloat("_EmissionMainStrength");

        var hasEmissionMap = emissionMap != null;
        var hasEmissionBlendMask = emissionBlendMask != null;
        var usesMainTextureAsEmission = emissionMainStrength > 0 && mainTexture != null;

        if (!hasEmissionMap && !hasEmissionBlendMask && !usesMainTextureAsEmission)
        {
            Xiexe.EmissionMap = Context.GetITexture2D(UnityEngine.Texture2D.whiteTexture);
            return;
        }

        // If the emission map is not UV0, baking will cause issues, so do not bake.
        if (hasEmissionMap && emissionMapUV != 0)
        {
            Xiexe.EmissionMap = Context.GetITexture2D(emissionMap);
            Xiexe.EmissionMapScale = Material.GetTextureScale("_EmissionMap");
            Xiexe.EmissionMapOffset = Material.GetTextureOffset("_EmissionMap");
            Xiexe.EmissionUV = emissionMapUV;
            return;
        }

        var bakerShader = UnityEngine.Shader.Find("Hidden/ltsother_baker");
        if (bakerShader == null)
        {
            UnityEngine.Debug.LogWarning("Could not find lilToon texture baker shader Hidden/ltsother_baker.");
            if (hasEmissionMap)
            {
                Xiexe.EmissionMap = Context.GetITexture2D(emissionMap);
                Xiexe.EmissionMapScale = Material.GetTextureScale("_EmissionMap");
                Xiexe.EmissionMapOffset = Material.GetTextureOffset("_EmissionMap");
                Xiexe.EmissionUV = emissionMapUV;
            }
            else
            {
                Xiexe.EmissionMap = Context.GetITexture2D(emissionBlendMask ?? mainTexture ?? UnityEngine.Texture2D.whiteTexture);
                Xiexe.EmissionMapScale = Vector2.one;
                Xiexe.EmissionMapOffset = Vector2.zero;
                Xiexe.EmissionUV = 0;
            }
            return;
        }

        UnityEngine.Texture2D bakedTexture = null;
        UnityEngine.Material bakerMaterial = null;

        try
        {
            bakerMaterial = new UnityEngine.Material(bakerShader);
            bakerMaterial.SetColor("_Color", Color.white);
            if (hasEmissionMap)
            {
                bakerMaterial.SetTexture("_MainTex", emissionMap);
                bakerMaterial.SetTextureScale("_MainTex", Material.GetTextureScale("_EmissionMap"));
                bakerMaterial.SetTextureOffset("_MainTex", Material.GetTextureOffset("_EmissionMap"));
            }
            else
            {
                bakerMaterial.SetTexture("_MainTex", UnityEngine.Texture2D.whiteTexture);
            }
            if (hasEmissionBlendMask)
            {
                bakerMaterial.SetFloat("_UseMain2ndTex", 1);
                bakerMaterial.SetColor("_Color2nd", Color.white);
                bakerMaterial.SetTexture("_Main2ndTex", emissionBlendMask);
                bakerMaterial.SetTextureScale("_Main2ndTex", Material.GetTextureScale("_EmissionBlendMask"));
                bakerMaterial.SetTextureOffset("_Main2ndTex", Material.GetTextureOffset("_EmissionBlendMask"));
                bakerMaterial.SetFloat("_Main2ndTexBlendMode", 3);  // Multiply
            }
            if (usesMainTextureAsEmission)
            {
                bakerMaterial.SetFloat("_UseMain3rdTex", 1);
                bakerMaterial.SetColor("_Color3rd", new Color(1, 1, 1, Mathf.Clamp01(emissionMainStrength)));
                bakerMaterial.SetTexture("_Main3rdTex", mainTexture);
                bakerMaterial.SetTextureScale("_Main3rdTex", mainTextureScale);
                bakerMaterial.SetTextureOffset("_Main3rdTex", mainTextureOffset);
                bakerMaterial.SetFloat("_Main3rdTexBlendMode", 3);  // Multiply
            }

            var sourceTexture2D = emissionMap as UnityEngine.Texture2D
                ?? emissionBlendMask as UnityEngine.Texture2D
                ?? mainTexture as UnityEngine.Texture2D
                ?? UnityEngine.Texture2D.whiteTexture;
            bakedTexture = BakeMaterialToTexture(sourceTexture2D, bakerMaterial, emissionMap.width, emissionMap.height);
            bakedTexture.name = $"{Material.name}_EmissionMap_Baked";
            bakedTexture.wrapMode = emissionMap.wrapMode;
            bakedTexture.filterMode = emissionMap.filterMode;
            bakedTexture.anisoLevel = emissionMap.anisoLevel;
        }
        catch (Exception exception)
        {
            if (bakedTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(bakedTexture);
            }

            UnityEngine.Debug.LogWarning($"Could not bake lilToon emission map. {exception.Message}");
            return;
        }
        finally
        {
            if (bakerMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(bakerMaterial);
            }
        }

        if (AssetCache.EmissionMap != null && AssetCache.EmissionMap != bakedTexture && !EditorUtility.IsPersistent(AssetCache.EmissionMap))
        {
            UnityEngine.Object.DestroyImmediate(AssetCache.EmissionMap);
        }

        AssetCache.EmissionMap = bakedTexture;
        Xiexe.EmissionMap = Context.GetITexture2D(bakedTexture);
        Xiexe.EmissionMapScale = Vector2.one;
        Xiexe.EmissionMapOffset = Vector2.zero;
        Xiexe.EmissionUV = 0;
    }

    private void UpdateOcclusion()
    {
        if (Material.GetFloat("_UseShadow") == 0)
        {
            Xiexe.OcclusionColor = Color.black.ToColorX_sRGB();
            return;
        }

        Xiexe.OcclusionMap = Context.GetITexture2D(Material.GetTexture("_ShadowBorderMask"));
        Xiexe.OcclusionMapScale = Material.GetTextureScale("_ShadowBorderMask");
        Xiexe.OcclusionMapOffset = Material.GetTextureOffset("_ShadowBorderMask");

        var shadowColor = Material.GetColor("_ShadowColor");
        var occlusionColor = Color.Lerp(Color.white, shadowColor, Material.GetFloat("_ShadowStrength"));
        Xiexe.OcclusionColor = occlusionColor.ToColorX_Auto();
    }

    private void UpdateOutline()
    {
        if (Material.GetFloat("_UseOutline") == 0 && !Material.shader.name.Contains("Outline", StringComparison.OrdinalIgnoreCase))
        {
            Xiexe.Outline = XiexeToonMaterial.OutlineStyle.None;
            return;
        }

        Xiexe.Outline = Material.GetFloat("_OutlineEnableLighting") > 0
            ? XiexeToonMaterial.OutlineStyle.Lit
            : XiexeToonMaterial.OutlineStyle.Emissive;
        Xiexe.OutlineWidth = Material.GetFloat("_OutlineWidth");
        Xiexe.OutlineColor = Material.GetColor("_OutlineColor").ToColorX_Auto();
        Xiexe.OutlineAlbedoTint = Material.GetFloat("_OutlineLitApplyTex") > 0;
        Xiexe.OutlineMask = Context.GetITexture2D(Material.GetTexture("_OutlineWidthMask"));
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

            bakedRamp = new UnityEngine.Texture2D(width, height, TextureFormat.RGBA32, false)
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

    private static UnityEngine.Texture2D BakeMaterialToTexture(Texture sourceTexture, UnityEngine.Material material, int width, int height)
    {
        UnityEngine.RenderTexture renderTexture = null;
        UnityEngine.Texture2D bakedTexture = null;
        var currentRenderTexture = UnityEngine.RenderTexture.active;

        try
        {
            renderTexture = UnityEngine.RenderTexture.GetTemporary(width, height);
            Graphics.Blit(sourceTexture, renderTexture, material);
            UnityEngine.RenderTexture.active = renderTexture;

            bakedTexture = new UnityEngine.Texture2D(width, height);
            bakedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            bakedTexture.Apply();

            return bakedTexture;
        }
        catch
        {
            if (bakedTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(bakedTexture);
            }

            throw;
        }
        finally
        {
            UnityEngine.RenderTexture.active = currentRenderTexture;
            if (renderTexture != null)
            {
                UnityEngine.RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }

}
