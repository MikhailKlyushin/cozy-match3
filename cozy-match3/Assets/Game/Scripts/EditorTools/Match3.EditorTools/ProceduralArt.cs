using System;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Rasterises placeholder art. Each chip colour gets its own silhouette AND its own
    /// lightness, because §17 requires the six chips to stay distinguishable in greyscale -
    /// six pastel shapes of equal value are unreadable and show up as a hint-usage spike (§14).
    /// </summary>
    internal static class ProceduralArt
    {
        internal const int ChipSize = 128;
        internal const int IconSize = 128;

        private const int Supersample = 3;

        /// <summary>Silhouette per colour index 1..6. Shape carries the identity, colour reinforces it.</summary>
        private enum ChipShape
        {
            Circle = 0,
            RoundedSquare = 1,
            Triangle = 2,
            Hexagon = 3,
            Diamond = 4,
            Star = 5,
        }

        private static readonly Color[] ChipFill =
        {
            new Color(1.00f, 0.86f, 0.72f), // 1 peach, lightest
            new Color(0.47f, 0.68f, 0.92f), // 2 blue, mid
            new Color(0.13f, 0.42f, 0.42f), // 3 teal, dark
            new Color(0.83f, 0.79f, 0.96f), // 4 lavender, light
            new Color(0.40f, 0.72f, 0.44f), // 5 green, mid
            new Color(0.44f, 0.16f, 0.29f), // 6 wine, darkest
        };

        internal static Color ChipColorOf(int colorIndex) => ChipFill[Mathf.Clamp(colorIndex, 1, 6) - 1];

        internal static Texture2D CreateChip(int colorIndex)
        {
            var shape = (ChipShape)(Mathf.Clamp(colorIndex, 1, 6) - 1);
            Color fill = ChipColorOf(colorIndex);
            Color outline = Darken(fill, 0.45f);
            Color highlight = Lighten(fill, 0.35f);

            var texture = NewTexture(ChipSize, "T_Chip_Cat0" + colorIndex.ToString() + "_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, ChipSize);

                float body = ShapeDistance(shape, p, 0.74f);
                float ears = EarsDistance(shape, p);
                float d = Mathf.Min(body, ears);

                if (d > 0f)
                {
                    return Color.clear;
                }

                if (d > -0.055f)
                {
                    return outline;
                }

                // Eyes and a muzzle dot keep the shapes readable at board scale.
                if (Circle(p, new Vector2(-0.22f, 0.10f), 0.075f) || Circle(p, new Vector2(0.22f, 0.10f), 0.075f))
                {
                    return outline;
                }

                if (Circle(p, new Vector2(0f, -0.14f), 0.05f))
                {
                    return Darken(fill, 0.25f);
                }

                float sheen = Mathf.Clamp01(0.5f + 0.5f * (p.y - p.x * 0.3f));
                return Color.Lerp(fill, highlight, sheen * 0.35f);
            });

            texture.Apply();
            return texture;
        }

        internal static Texture2D CreateRocket(bool horizontal)
        {
            Color fill = new Color(0.96f, 0.55f, 0.24f);
            Color outline = Darken(fill, 0.5f);
            var texture = NewTexture(IconSize, horizontal ? "T_Booster_RocketH_2D" : "T_Booster_RocketV_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);
                Vector2 q = horizontal ? new Vector2(p.y, p.x) : p;

                // Nose cone above, body below, two fins.
                bool nose = q.y > 0.25f && Mathf.Abs(q.x) < (0.85f - q.y) * 0.55f;
                bool body = q.y <= 0.25f && q.y > -0.55f && Mathf.Abs(q.x) < 0.24f;
                bool fins = q.y <= -0.15f && q.y > -0.62f && Mathf.Abs(q.x) < 0.24f + (-0.15f - q.y) * 0.9f;
                bool inside = nose || body || fins;
                if (!inside)
                {
                    return Color.clear;
                }

                bool edge = !(nose && Mathf.Abs(q.x) < (0.85f - q.y) * 0.4f)
                            && !(body && Mathf.Abs(q.x) < 0.17f)
                            && !(fins && Mathf.Abs(q.x) < 0.17f + (-0.15f - q.y) * 0.7f);
                return edge ? outline : fill;
            });

            texture.Apply();
            return texture;
        }

        internal static Texture2D CreateBomb()
        {
            Color fill = new Color(0.28f, 0.28f, 0.34f);
            Color outline = Darken(fill, 0.5f);
            Color fuse = new Color(0.98f, 0.78f, 0.28f);
            var texture = NewTexture(IconSize, "T_Booster_Bomb_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);

                if (Circle(p, new Vector2(0.30f, 0.66f), 0.12f))
                {
                    return fuse;
                }

                Vector2 c = p - new Vector2(0f, -0.06f);
                float d = c.magnitude - 0.66f;
                if (d > 0f)
                {
                    return Color.clear;
                }

                if (d > -0.06f)
                {
                    return outline;
                }

                return Circle(p, new Vector2(-0.20f, 0.18f), 0.13f) ? Lighten(fill, 0.5f) : fill;
            });

            texture.Apply();
            return texture;
        }

        internal static Texture2D CreateRainbow()
        {
            var texture = NewTexture(IconSize, "T_Booster_Rainbow_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);
                float d = p.magnitude - 0.72f;
                if (d > 0f)
                {
                    return Color.clear;
                }

                if (d > -0.06f)
                {
                    return new Color(0.15f, 0.15f, 0.18f);
                }

                // Six wedges, one per colour index: the ball reads as "any colour".
                float angle = Mathf.Atan2(p.y, p.x) + Mathf.PI;
                int wedge = Mathf.Clamp((int)(angle / (Mathf.PI * 2f) * 6f), 0, 5);
                return ChipFill[wedge];
            });

            texture.Apply();
            return texture;
        }

        internal static Texture2D CreateAirplane()
        {
            Color fill = new Color(0.93f, 0.95f, 0.99f);
            Color outline = new Color(0.35f, 0.42f, 0.55f);
            var texture = NewTexture(IconSize, "T_Booster_Airplane_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);

                // Paper dart: a forward triangle with a notched tail.
                bool dart = p.y < 0.72f && p.y > -0.72f
                            && Mathf.Abs(p.x) < (0.72f - p.y) * 0.52f;
                bool notch = p.y < -0.34f && Mathf.Abs(p.x) < (-0.34f - p.y) * 0.75f;
                if (!dart || notch)
                {
                    return Color.clear;
                }

                bool crease = Mathf.Abs(p.x) < 0.035f;
                bool edge = Mathf.Abs(p.x) > (0.72f - p.y) * 0.52f - 0.05f;
                return crease || edge ? outline : fill;
            });

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Cardboard box. <paramref name="damageStage"/> 0 is pristine; each further stage adds
        /// visible creases, because §7.1 requires the visual to change on every hit point lost.
        /// </summary>
        internal static Texture2D CreateBox(int damageStage, string assetName)
        {
            Color fill = new Color(0.78f, 0.60f, 0.38f);
            Color outline = Darken(fill, 0.5f);
            Color crease = Darken(fill, 0.28f);
            var texture = NewTexture(IconSize, assetName);

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);
                float d = BoxDistance(p, new Vector2(0.76f, 0.76f), 0.08f);
                if (d > 0f)
                {
                    return Color.clear;
                }

                if (d > -0.055f)
                {
                    return outline;
                }

                if (Mathf.Abs(p.y - 0.18f) < 0.035f || Mathf.Abs(p.x) < 0.035f && p.y < 0.18f)
                {
                    return crease;
                }

                for (int i = 0; i < damageStage; i++)
                {
                    float offset = -0.42f + i * 0.34f;
                    if (Mathf.Abs(p.y - p.x * 0.55f - offset) < 0.045f)
                    {
                        return crease;
                    }
                }

                return fill;
            });

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Ribbon mask for the coloured boxes, drawn white so the view can tint it from the chip
        /// palette. Kept separate from the box so cx works for any colorCount (§7.2).
        /// </summary>
        internal static Texture2D CreateBoxBow()
        {
            var texture = NewTexture(IconSize, "T_Element_BoxBow_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);
                if (BoxDistance(p, new Vector2(0.76f, 0.76f), 0.08f) > -0.055f)
                {
                    return Color.clear;
                }

                if (Circle(p, Vector2.zero, 0.22f))
                {
                    return Circle(p, Vector2.zero, 0.16f) ? Color.white : new Color(0f, 0f, 0f, 0.55f);
                }

                bool ribbon = Mathf.Abs(p.x) < 0.12f || Mathf.Abs(p.y) < 0.12f;
                return ribbon ? Color.white : Color.clear;
            });

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Next-colour pip of the cx box, white for runtime tinting. Without it the box reads as
        /// random; with it the mechanic becomes plannable (§7.2, Q5 = on).
        /// </summary>
        internal static Texture2D CreateBoxPip()
        {
            var texture = NewTexture(64, "T_Element_BoxPip_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, 64);
                float d = p.magnitude - 0.88f;
                if (d > 0f)
                {
                    return Color.clear;
                }

                return d > -0.22f ? new Color(0f, 0f, 0f, 0.7f) : Color.white;
            });

            texture.Apply();
            return texture;
        }

        /// <summary>Blocker: a stone slab with rivets, deliberately reading as "not breakable".</summary>
        internal static Texture2D CreateBlocker()
        {
            Color fill = new Color(0.42f, 0.44f, 0.48f);
            Color outline = Darken(fill, 0.55f);
            Color rivet = Lighten(fill, 0.35f);
            var texture = NewTexture(IconSize, "T_Element_Blocker_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);
                float d = BoxDistance(p, new Vector2(0.82f, 0.82f), 0.05f);
                if (d > 0f)
                {
                    return Color.clear;
                }

                if (d > -0.07f)
                {
                    return outline;
                }

                if (Circle(p, new Vector2(-0.5f, 0.5f), 0.10f) || Circle(p, new Vector2(0.5f, 0.5f), 0.10f)
                    || Circle(p, new Vector2(-0.5f, -0.5f), 0.10f) || Circle(p, new Vector2(0.5f, -0.5f), 0.10f))
                {
                    return rivet;
                }

                bool hatch = Mathf.Abs(Mathf.Repeat(p.x + p.y, 0.34f) - 0.17f) < 0.045f;
                return hatch ? Darken(fill, 0.18f) : fill;
            });

            texture.Apply();
            return texture;
        }

        /// <summary>Rounded cell tile for the board background.</summary>
        internal static Texture2D CreateCellTile()
        {
            var texture = NewTexture(IconSize, "T_Board_Cell_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);
                float d = BoxDistance(p, new Vector2(0.92f, 0.92f), 0.16f);
                if (d > 0f)
                {
                    return Color.clear;
                }

                return d > -0.05f
                    ? new Color(1f, 1f, 1f, 0.10f)
                    : new Color(1f, 1f, 1f, 0.05f);
            });

            texture.Apply();
            return texture;
        }

        /// <summary>Solid rounded panel used by HUD and popups, tinted per use through Image.color.</summary>
        internal static Texture2D CreatePanel()
        {
            var texture = NewTexture(IconSize, "T_Ui_Panel_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, IconSize);
                float d = BoxDistance(p, new Vector2(0.94f, 0.94f), 0.28f);
                return d > 0f ? Color.clear : Color.white;
            });

            texture.Apply();
            return texture;
        }

        /// <summary>Soft radial blob used for FX particles and glows.</summary>
        internal static Texture2D CreateGlow()
        {
            var texture = NewTexture(64, "T_Fx_Glow_2D");

            Rasterise(texture, (x, y) =>
            {
                Vector2 p = ToUnit(x, y, 64);
                float a = Mathf.Clamp01(1f - p.magnitude);
                return new Color(1f, 1f, 1f, a * a);
            });

            texture.Apply();
            return texture;
        }

        private static Texture2D NewTexture(int size, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            texture.name = name;
            return texture;
        }

        /// <summary>Supersampled rasterisation: the shader-free way to get clean silhouettes.</summary>
        private static void Rasterise(Texture2D texture, Func<float, float, Color> sample)
        {
            int size = texture.width;
            var pixels = new Color[size * size];
            float step = 1f / Supersample;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = 0f;
                    float g = 0f;
                    float b = 0f;
                    float a = 0f;

                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            Color c = sample(x + (sx + 0.5f) * step, y + (sy + 0.5f) * step);
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }

                    int samples = Supersample * Supersample;
                    if (a <= 0.0001f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    pixels[y * size + x] = new Color(r / a, g / a, b / a, a / samples);
                }
            }

            texture.SetPixels(pixels);
        }

        /// <summary>Pixel coordinates to [-1, 1] with y up.</summary>
        private static Vector2 ToUnit(float x, float y, int size)
            => new Vector2(x / size * 2f - 1f, y / size * 2f - 1f);

        private static bool Circle(Vector2 p, Vector2 centre, float radius)
            => (p - centre).sqrMagnitude <= radius * radius;

        /// <summary>Negative inside. Rounded box SDF.</summary>
        private static float BoxDistance(Vector2 p, Vector2 half, float rounding)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - new Vector2(rounding, rounding));
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                   + Mathf.Min(Mathf.Max(q.x, q.y), 0f)
                   - rounding;
        }

        /// <summary>Negative inside. Distance is approximate but monotonic, which is all the outline needs.</summary>
        private static float ShapeDistance(ChipShape shape, Vector2 p, float radius)
        {
            switch (shape)
            {
                case ChipShape.Circle:
                    return p.magnitude - radius;

                case ChipShape.RoundedSquare:
                    return BoxDistance(p, new Vector2(radius, radius), radius * 0.35f);

                case ChipShape.Triangle:
                    return TriangleDistance(p, radius);

                case ChipShape.Hexagon:
                    return PolygonDistance(p, radius, 6, Mathf.PI / 6f);

                case ChipShape.Diamond:
                    return (Mathf.Abs(p.x) + Mathf.Abs(p.y)) * 0.72f - radius;

                case ChipShape.Star:
                    return StarDistance(p, radius);

                default:
                    return p.magnitude - radius;
            }
        }

        private static float TriangleDistance(Vector2 p, float radius)
        {
            // Upright triangle, nudged down so the centroid sits near the cell centre.
            Vector2 q = new Vector2(p.x, p.y + radius * 0.22f);
            float d = -radius;
            for (int i = 0; i < 3; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI * 2f / 3f;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                d = Mathf.Max(d, Vector2.Dot(q, normal) - radius * 0.62f);
            }

            return d;
        }

        private static float PolygonDistance(Vector2 p, float radius, int sides, float rotation)
        {
            float d = float.NegativeInfinity;
            for (int i = 0; i < sides; i++)
            {
                float angle = rotation + i * Mathf.PI * 2f / sides;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                d = Mathf.Max(d, Vector2.Dot(p, normal) - radius * 0.92f);
            }

            return d;
        }

        private static float StarDistance(Vector2 p, float radius)
        {
            float angle = Mathf.Atan2(p.y, p.x);
            float modulated = radius * (0.62f + 0.38f * Mathf.Abs(Mathf.Cos(angle * 2.5f)));
            return p.magnitude - modulated;
        }

        /// <summary>Two ears on top: the shared cue that every shape is a cat.</summary>
        private static float EarsDistance(ChipShape shape, Vector2 p)
        {
            float lift = shape == ChipShape.Triangle ? 0.30f : 0.46f;
            float left = EarDistance(p, new Vector2(-0.40f, lift));
            float right = EarDistance(p, new Vector2(0.40f, lift));
            return Mathf.Min(left, right);
        }

        private static float EarDistance(Vector2 p, Vector2 baseCentre)
        {
            Vector2 q = p - baseCentre;
            float height = 0.34f;
            if (q.y < 0f || q.y > height)
            {
                return 1f;
            }

            float halfWidth = 0.20f * (1f - q.y / height);
            return Mathf.Abs(q.x) - halfWidth;
        }

        private static Color Darken(Color c, float amount)
            => new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);

        private static Color Lighten(Color c, float amount)
            => new Color(
                Mathf.Lerp(c.r, 1f, amount),
                Mathf.Lerp(c.g, 1f, amount),
                Mathf.Lerp(c.b, 1f, amount),
                c.a);
    }
}
