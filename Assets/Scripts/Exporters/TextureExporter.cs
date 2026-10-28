using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json.Linq;
using System;

public class TextureExporter
{
    static public Texture2D duplicateTexture(Texture2D source)
    {
        RenderTexture renderTex = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        Texture2D readableText = new Texture2D(source.width, source.height);
        readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableText.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);
        return readableText;
    }

    static readonly string[] MaskColorChannels = { "R", "G", "B" };

    /// <summary> Mob materials tint a near-white _MainTex in the shader using _MaskColorTex and _MaskColor* (USE_MASK_COLOR). </summary>
    static public bool IsMaskColorMaterial(Material mat)
    {
        return mat.IsKeywordEnabled("USE_MASK_COLOR")
            && mat.HasProperty("_MaskColorTex")
            && mat.GetTexture("_MaskColorTex") is Texture2D;
    }

    /// <summary> File name (without extension) the material's main texture is exported under. </summary>
    static public string GetMainTextureFileName(Material mat)
    {
        string name = mat.mainTexture.name;
        if (!IsMaskColorMaterial(mat))
        {
            return name;
        }

        // Mob textures are shared between many mobs, so the baked copy is keyed by its colors.
        // Prefixed rather than suffixed so name checks like EndsWith("eye0") still apply.
        uint hash = 2166136261;
        foreach (var c in MaskColorChannels)
        {
            foreach (var n in new[] { "1", "2" })
            {
                foreach (char ch in ColorUtility.ToHtmlStringRGB(mat.GetColor($"_MaskColor{c}{n}")))
                {
                    hash = (hash ^ ch) * 16777619;
                }
            }
        }
        return $"mask{hash:x8}_{name}";
    }

    /// <summary>
    /// Approximates the game's USE_MASK_COLOR tint (the shader source is not available).
    /// Each _MaskColorTex channel is 0.5 where unused; values towards 1 tint with color 2,
    /// values towards 0 tint with color 1.
    /// </summary>
    static Texture2D BakeMaskColor(Texture2D diff, Material mat)
    {
        Texture2D mask = duplicateTexture((Texture2D)mat.GetTexture("_MaskColorTex"));
        Color[] color1 = new Color[3];
        Color[] color2 = new Color[3];
        for (int c = 0; c < 3; c++)
        {
            color1[c] = mat.GetColor($"_MaskColor{MaskColorChannels[c]}1");
            color2[c] = mat.GetColor($"_MaskColor{MaskColorChannels[c]}2");
        }

        Color[] pixels = diff.GetPixels();
        int width = diff.width, height = diff.height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                Color m = mask.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
                Color tint = Color.white;
                for (int c = 0; c < 3; c++)
                {
                    float v = m[c];
                    float weight = Mathf.Clamp01(Mathf.Abs(v - 0.5f) * 2);
                    tint *= Color.Lerp(Color.white, v > 0.5f ? color2[c] : color1[c], weight);
                }
                Color p = pixels[i];
                pixels[i] = new Color(p.r * tint.r, p.g * tint.g, p.b * tint.b, p.a);
            }
        }

        Texture2D baked = new Texture2D(width, height);
        baked.SetPixels(pixels);
        baked.Apply();
        UnityEngine.Object.Destroy(mask);
        return baked;
    }

    static public string[] ExportAllTexture(string path, GameObject gameobj)
    {
        string savePath = path + "/Texture2D/";
        List<string> textureNames = new List<string>();
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        var owner = gameobj.GetComponent<UmaContainer>();
        foreach (Renderer renderer in gameobj.transform.GetComponentsInChildren<Renderer>())
        {
            if (owner != null && UmaContainerProp.IsAttachedPropRenderer(renderer, owner)) continue;

            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;

                // 获取材质的所有属性；如果某个属性对应纹理，则提取该纹理
                Shader mat_shader = mat.shader;
                int p_num = mat_shader.GetPropertyCount();
                for (int i = 0; i < p_num; i++)
                {
                    string p_name = mat_shader.GetPropertyName(i);
                    if (mat_shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                    {
                        // Scene materials also hold cubemaps/RenderTextures, which can't be exported as PNG.
                        Texture2D mat_texture = mat.GetTexture(p_name) as Texture2D;

                        if (mat_texture)
                        {
                            bool bakeMask = p_name == "_MainTex" && IsMaskColorMaterial(mat);
                            string tex_name = bakeMask ? GetMainTextureFileName(mat) : mat_texture.name;
                            string tex_path = savePath + tex_name + ".png";
                            var dpath = "Texture2D/" + tex_name + ".png";
                            if (!textureNames.Contains(dpath))
                            {
                                textureNames.Add(dpath);
                            }
                            if (!File.Exists(tex_path))
                            {   

                                mat_texture = duplicateTexture(mat_texture);
                                if (bakeMask)
                                {
                                    mat_texture = BakeMaskColor(mat_texture, mat);
                                }

                                if (tex_name.EndsWith("eye0"))
                                {
                                    var clip_texture = duplicateTexture(mat_texture);
                                    var pixels = clip_texture.GetPixels(0, 0, clip_texture.width / 4, clip_texture.height);
                                    clip_texture.Reinitialize(clip_texture.width / 4, clip_texture.height);
                                    clip_texture.SetPixels(pixels);


                                    byte[] clip = clip_texture.EncodeToPNG();
                                    File.WriteAllBytes(tex_path, clip);
                                    tex_path = savePath + tex_name + "_all" + ".png";
                                }

                                byte[] bytes = mat_texture.EncodeToPNG();
                                File.WriteAllBytes(tex_path, bytes);
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("纹理提取成功。");
        return textureNames.ToArray();
    }
}
