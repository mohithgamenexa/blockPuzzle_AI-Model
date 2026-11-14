using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Generates every sprite procedurally so the project needs no imported art.
    /// Block = grey bevelled "candy" tile (tinted per colour) + white gloss overlay.
    /// </summary>
    public static class SpriteFactory
    {
        const int BlockRes = 128;

        static Sprite _blockBase, _blockGloss, _cellSlot, _panel, _glow, _pixel, _gradient, _ring, _restartIcon, _sound, _muted;

        public static Sprite BlockBase => _blockBase ??= Make(BuildBlockBase(), BlockRes);
        public static Sprite BlockGloss => _blockGloss ??= Make(BuildBlockGloss(), BlockRes);
        public static Sprite CellSlot => _cellSlot ??= Make(BuildCellSlot(), BlockRes);
        public static Sprite Glow => _glow ??= Make(BuildGlow(64), 64);
        public static Sprite Pixel => _pixel ??= Make(Solid(4, 4), 4);
        public static Sprite RestartIcon => _restartIcon ??= Make(BuildRestartIcon(128), 128);
        public static Sprite Ring => _ring ??= Make(BuildRing(128), 128);
        public static Sprite SoundIcon => _sound ??= Make(BuildNoteIcon(128, false), 128);
        public static Sprite MutedIcon => _muted ??= Make(BuildNoteIcon(128, true), 128);

        /// <summary>Rounded rectangle for 9-slicing (world SpriteRenderers and UI Images).</summary>
        public static Sprite Panel
        {
            get
            {
                if (_panel != null) return _panel;
                const int size = 96, radius = 32;
                var tex = NewTexture(size, size);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = RoundedBox(x + 0.5f - size / 2f, y + 0.5f - size / 2f, size / 2f, size / 2f, radius);
                        px[y * size + x] = new Color32(255, 255, 255, ToByte(Mathf.Clamp01(0.5f - d)));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _panel = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0,
                    SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
                return _panel;
            }
        }

        /// <summary>Vertical gradient used as the scene background.</summary>
        public static Sprite Gradient
        {
            get
            {
                if (_gradient != null) return _gradient;
                const int h = 256;
                var tex = NewTexture(2, h);
                for (int y = 0; y < h; y++)
                {
                    float t = y / (h - 1f);
                    var c = Color.Lerp(Palette.BackgroundBottom, Palette.BackgroundTop, t * t * (3f - 2f * t));
                    tex.SetPixel(0, y, c);
                    tex.SetPixel(1, y, c);
                }
                tex.Apply(false, true);
                _gradient = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 2, 0, SpriteMeshType.FullRect);
                return _gradient;
            }
        }

        // ---------------------------------------------------------------- builders

        static readonly Vector2 LightDir = new Vector2(-0.45f, 0.89f).normalized;

        static Color32[] BuildBlockBase()
        {
            int n = BlockRes;
            float half = n / 2f, radius = n * 0.2f, bevel = n * 0.12f;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f - half, fy = y + 0.5f - half;
                    float d = RoundedBox(fx, fy, half - 1f, half - 1f, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }

                    float inside = -d;
                    float v = y / (float)n;
                    float face = 0.80f + 0.08f * v;                       // subtle top-lit gradient

                    Vector2 normal = Gradient2(fx, fy, half - 1f, radius);
                    float lit = Vector2.Dot(normal, LightDir);           // -1..1
                    float bevelShade = 0.80f + 0.20f * lit;

                    float k = Smooth(bevel - 2f, bevel + 1f, inside);    // 0 at edge -> 1 at face
                    float value = Mathf.Lerp(bevelShade, face, k);
                    value *= Mathf.Lerp(0.72f, 1f, Smooth(0f, 3f, inside)); // dark outer rim for definition

                    byte b = ToByte(value);
                    px[y * n + x] = new Color32(b, b, b, ToByte(alpha));
                }
            return px;
        }

        static Color32[] BuildBlockGloss()
        {
            int n = BlockRes;
            float half = n / 2f, radius = n * 0.2f, bevel = n * 0.12f;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f - half, fy = y + 0.5f - half;
                    float d = RoundedBox(fx, fy, half - 1f, half - 1f, radius);
                    if (d > 0.5f) { px[y * n + x] = new Color32(255, 255, 255, 0); continue; }
                    float inside = -d;
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;

                    // Rim light on the bevel facing the light.
                    Vector2 normal = Gradient2(fx, fy, half - 1f, radius);
                    float lit = Mathf.Max(0f, Vector2.Dot(normal, LightDir));
                    float rim = lit * (1f - Smooth(bevel - 3f, bevel, inside)) * Smooth(0f, 2f, inside) * 0.55f;

                    // Soft glossy band across the upper face.
                    float ex = (u - 0.5f) / 0.33f, ey = (v - 0.70f) / 0.13f;
                    float band = (1f - Smooth(0.75f, 1f, ex * ex + ey * ey)) * 0.22f;

                    // Small specular highlight near the top-left.
                    float sx = (u - 0.27f) / 0.07f, sy = (v - 0.76f) / 0.05f;
                    float spec = (1f - Smooth(0.4f, 1f, sx * sx + sy * sy)) * 0.75f;

                    float a = Mathf.Clamp01(Mathf.Max(rim, band + spec)) * Mathf.Clamp01(0.5f - d);
                    px[y * n + x] = new Color32(255, 255, 255, ToByte(a));
                }
            return px;
        }

        static Color32[] BuildCellSlot()
        {
            int n = BlockRes;
            float half = n / 2f, radius = n * 0.18f;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f - half, fy = y + 0.5f - half;
                    float d = RoundedBox(fx, fy, half - 4f, half - 4f, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    // Inset look: darker band along the top inner edge.
                    float inside = -d;
                    float topness = Mathf.Clamp01(fy / half);
                    float shade = 1f - 0.35f * topness * (1f - Smooth(0f, n * 0.1f, inside));
                    byte b = ToByte(shade);
                    px[y * n + x] = new Color32(b, b, b, ToByte(alpha));
                }
            return px;
        }

        static Color32[] BuildGlow(int n)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    px[y * n + x] = new Color32(255, 255, 255, ToByte(a * a));
                }
            return px;
        }

        static Color32[] BuildRing(int n)
        {
            var px = new Color32[n * n];
            float half = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                    float a = Mathf.Clamp01(0.5f - Mathf.Abs(r - half * 0.85f) + half * 0.06f);
                    px[y * n + x] = new Color32(255, 255, 255, ToByte(a));
                }
            return px;
        }

        /// <summary>Circular arrow: a 300° arc with an arrowhead at its end.</summary>
        static Color32[] BuildRestartIcon(int n)
        {
            var px = new Color32[n * n];
            float half = n / 2f, radius = n * 0.30f, thick = n * 0.055f;
            const float gapStart = 60f, gapEnd = 110f; // degrees with no arc (top-right)
            float endAngle = gapStart * Mathf.Deg2Rad;
            var tip = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radius;
            var tangent = new Vector2(Mathf.Sin(endAngle), -Mathf.Cos(endAngle)); // clockwise direction
            var outward = tip.normalized;
            float head = n * 0.13f;
            Vector2 a = tip + outward * head, b = tip - outward * head, c = tip - tangent * head * 1.2f;

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                    float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    if (ang < 0) ang += 360f;
                    float arc = (ang > gapStart && ang < gapEnd) ? 0f : Mathf.Clamp01(thick - Mathf.Abs(p.magnitude - radius) + 0.5f);
                    float tri = InTriangle(p, a, b, c);
                    px[y * n + x] = new Color32(255, 255, 255, ToByte(Mathf.Max(arc, tri)));
                }
            return px;
        }

        /// <summary>Beamed pair of music notes; optionally struck through for "muted".</summary>
        static Color32[] BuildNoteIcon(int n, bool struck)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float a = 0f;
                    a = Mathf.Max(a, Ellipse(u, v, 0.33f, 0.30f, 0.10f, 0.075f));
                    a = Mathf.Max(a, Ellipse(u, v, 0.67f, 0.36f, 0.10f, 0.075f));
                    a = Mathf.Max(a, Box(u, v, 0.405f, 0.445f, 0.30f, 0.76f, n));
                    a = Mathf.Max(a, Box(u, v, 0.745f, 0.785f, 0.36f, 0.82f, n));
                    // Slanted beam joining the stems.
                    float beamY = Mathf.Lerp(0.70f, 0.76f, Mathf.InverseLerp(0.405f, 0.785f, u));
                    if (u >= 0.405f && u <= 0.785f) a = Mathf.Max(a, Mathf.Clamp01((0.055f - Mathf.Abs(v - beamY - 0.03f)) * n));
                    if (struck)
                    {
                        float dist = Mathf.Abs(u - v) / 1.4142f;
                        if (u > 0.12f && u < 0.88f) a = Mathf.Max(a, Mathf.Clamp01((0.045f - dist) * n));
                    }
                    px[y * n + x] = new Color32(255, 255, 255, ToByte(a));
                }
            return px;
        }

        static float Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx, dy = (v - cy) / ry;
            return Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 12f);
        }

        static float Box(float u, float v, float x0, float x1, float y0, float y1, int n)
        {
            float dx = Mathf.Min(u - x0, x1 - u), dy = Mathf.Min(v - y0, y1 - v);
            return Mathf.Clamp01(Mathf.Min(dx, dy) * n + 0.5f);
        }

        // ---------------------------------------------------------------- helpers

        static Sprite Make(Color32[] pixels, int size)
        {
            var tex = NewTexture(size, size);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        static Color32[] Solid(int w, int h)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            return px;
        }

        static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };

        static float RoundedBox(float px, float py, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px) - hx + r, qy = Mathf.Abs(py) - hy + r;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - r;
        }

        /// <summary>Outward surface normal of the rounded box (numerical gradient).</summary>
        static Vector2 Gradient2(float x, float y, float h, float r)
        {
            const float e = 0.75f;
            float gx = RoundedBox(x + e, y, h, h, r) - RoundedBox(x - e, y, h, h, r);
            float gy = RoundedBox(x, y + e, h, h, r) - RoundedBox(x, y - e, h, h, r);
            var g = new Vector2(gx, gy);
            return g.sqrMagnitude < 1e-6f ? Vector2.zero : g.normalized;
        }

        static float InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Edge(p, a, b), d2 = Edge(p, b, c), d3 = Edge(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return neg && pos ? 0f : 1f;
        }

        static float Edge(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static byte ToByte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
    }
}
