using System;
using UnityEngine;

namespace Forewarned.UI
{
    /// <summary>White sprites drawn in code and tinted: the ⚠ triangle (Valheim's fonts may lack the glyph),
    /// a plain white square for bars, and a soft-edged vignette for the edge flash.</summary>
    internal static class Sprites
    {
        private static Sprite _warning;
        private static Sprite _white;
        private static Sprite _vignette;

        private static readonly Vector2[] Triangle = { new Vector2(16f, 30f), new Vector2(31f, 2f), new Vector2(1f, 2f) };

        public static Sprite Warning => _warning ?? (_warning = Make(32, (u, v) => Coverage(u * 32f, v * 32f)));
        public static Sprite White => _white ?? (_white = Make(4, (u, v) => 1f));
        public static Sprite Vignette => _vignette ?? (_vignette = Make(128, VignetteAlpha));

        /// <summary>Fraction of a 4×4 supersample grid inside the triangle and outside its "!" cut-out.</summary>
        private static float Coverage(float px, float py)
        {
            int hits = 0;
            for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float x = (float)Math.Floor(px) + (sx + 0.5f) / 4f, y = (float)Math.Floor(py) + (sy + 0.5f) / 4f;
                    bool bar = x >= 14.5f && x <= 17.5f && y >= 12f && y <= 23f;
                    bool dot = x >= 14.5f && x <= 17.5f && y >= 6f && y <= 9f;
                    if (Inside(x, y, Triangle) && !bar && !dot)
                        hits++;
                }
            return hits / 16f;
        }

        /// <summary>Strong at the screen edges, clear from 20% inwards.</summary>
        private static float VignetteAlpha(float u, float v)
        {
            float edge = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            float a = 1f - edge / 0.2f;
            return a <= 0f ? 0f : a * a;
        }

        private static bool Inside(float x, float y, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > y) != (poly[j].y > y) &&
                    x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        /// <param name="alpha">Alpha for a pixel, given its centre as u, v in 0..1 (v up).</param>
        private static Sprite Make(int size, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = alpha((x + 0.5f) / size, (y + 0.5f) / size);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(a)));
                }
            tex.SetPixels32(px);
            tex.Apply();
            Sprite s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
