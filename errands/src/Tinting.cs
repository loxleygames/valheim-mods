using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Errands
{
    /// Keyed recolour. Haldor is one material, one atlas: skin, cloth, gold beads, cup. A hue shift on the
    /// shader turns everything, so instead the atlas is copied once per biome and only pixels near the
    /// cloth's hue are moved. Skin and gold stay as they are.
    public static class Tinting
    {
        private static readonly Dictionary<string, Texture2D> s_cache = new Dictionary<string, Texture2D>();
        private static bool? s_flipped;

        public static void Apply(GameObject go, Heightmap.Biome biome)
        {
            if (!TryParse(ErrandsPlugin.Tints.TryGetValue(biome, out var cfg) ? cfg.Value : "", out var dh, out var ds, out var dv)) return;
            if (dh == 0f && ds == 0f && dv == 0f) return;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.materials)
                {
                    if (!m || !(m.mainTexture is Texture2D src)) continue;
                    var tinted = Recolour(biome, src, dh, ds, dv);
                    if (tinted) m.mainTexture = tinted;
                }
            }
        }

        private static Texture2D Recolour(Heightmap.Biome biome, Texture2D src, float dh, float ds, float dv)
        {
            var cacheKey = src.name + "|" + biome;
            if (s_cache.TryGetValue(cacheKey, out var cached)) return cached;

            var readable = ReadBack(src);
            if (!readable) return null;

            float key = ErrandsPlugin.KeyHue.Value, tol = ErrandsPlugin.KeyTolerance.Value, minSat = ErrandsPlugin.KeyMinSaturation.Value;
            var px = readable.GetPixels();
            int moved = 0;
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out var h, out var s, out var v);
                if (s < minSat || HueDistance(h, key) > tol) continue;
                var c = Color.HSVToRGB(Mathf.Repeat(h + dh, 1f), Mathf.Clamp01(s + ds), Mathf.Clamp01(v + dv));
                c.a = px[i].a;
                px[i] = c;
                moved++;
            }
            readable.SetPixels(px);
            readable.Apply(true);
            readable.name = src.name + "_" + biome;
            Jotunn.Logger.LogDebug($"Errands: tinted {moved}/{px.Length} px of {src.name} for {biome}");
            s_cache[cacheKey] = readable;
            return readable;
        }

        public static float HueDistance(float a, float b)
        {
            float d = Mathf.Abs(a - b);
            return d > 0.5f ? 1f - d : d;
        }

        /// Copy a non-readable texture through a RenderTexture. Whether the copy lands upside down
        /// depends on the graphics API, so a 2-pixel probe is read the same way once to find out.
        public static Texture2D ReadBack(Texture2D src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            if (IsFlipped()) Flip(tex);
            tex.Apply(true);
            return tex;
        }

        private static bool IsFlipped()
        {
            if (s_flipped.HasValue) return s_flipped.Value;
            var probe = new Texture2D(1, 2, TextureFormat.RGBA32, false);
            probe.SetPixel(0, 0, Color.black);
            probe.SetPixel(0, 1, Color.white);
            probe.Apply();
            var rt = RenderTexture.GetTemporary(1, 2, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(probe, rt);
            RenderTexture.active = rt;
            var back = new Texture2D(1, 2, TextureFormat.RGBA32, false);
            back.ReadPixels(new Rect(0, 0, 1, 2), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            s_flipped = back.GetPixel(0, 0).r > 0.5f;
            Object.Destroy(probe);
            Object.Destroy(back);
            return s_flipped.Value;
        }

        private static void Flip(Texture2D tex)
        {
            var px = tex.GetPixels();
            int w = tex.width, h = tex.height;
            var flipped = new Color[px.Length];
            for (int y = 0; y < h; y++) System.Array.Copy(px, y * w, flipped, (h - 1 - y) * w, w);
            tex.SetPixels(flipped);
        }

        public static bool TryParse(string s, out float h, out float sat, out float v)
        {
            h = sat = v = 0f;
            var parts = s.Split(',');
            return parts.Length == 3
                && float.TryParse(parts[0].Trim(), out h)
                && float.TryParse(parts[1].Trim(), out sat)
                && float.TryParse(parts[2].Trim(), out v);
        }

        /// Write every material's main texture on the object as PNG beside the DLL, for picking the key.
        public static List<string> Dump(GameObject go, string dir)
        {
            var written = new List<string>();
            var seen = new HashSet<Texture>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (!m || !(m.mainTexture is Texture2D src) || !seen.Add(src)) continue;
                    var tex = ReadBack(src);
                    var path = Path.Combine(dir, $"{src.name}.png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.Destroy(tex);
                    written.Add($"{path} ({m.name} / {m.shader.name})");
                }
            return written;
        }
    }
}
