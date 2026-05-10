using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// HSL color picker with a hue ring and an inscribed S/L gradient triangle.
    /// Vertices of the triangle are: pure hue (top), black (bottom-left),
    /// white (bottom-right). Click-drag the ring to choose hue; click-drag the
    /// triangle to choose saturation/lightness.
    /// </summary>
    public static class HSLColorPicker
    {
        private const int RingTextureSize = 256;
        private const int TriangleTextureSize = 192;
        private const float RingThicknessRatio = 0.18f;
        private const float TriangleInsetRatio = 0.92f;

        private static Texture2D _hueRingTex;
        private static Texture2D _triangleTex;
        private static float _cachedTriangleHue = -1f;
        // Per-pixel barycentric weights for the triangle texture, packed as
        // (a, b, c) triples. Computed once and reused across hue changes.
        private static Vector3[] _triangleBarycentricCache;
        private static int _cachedBarycentricSize;

        private enum DragMode { None, Ring, Triangle }
        // Drag state is global because Unity's GUIUtility.hotControl is global —
        // only one HSL picker can be the active drag target at a time across
        // all editor windows.
        private static DragMode _dragMode = DragMode.None;

        [InitializeOnLoadMethod]
        private static void RegisterDomainReloadCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseCachedTextures;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseCachedTextures;
        }

        private static void ReleaseCachedTextures()
        {
            if (_hueRingTex != null)
            {
                UnityEngine.Object.DestroyImmediate(_hueRingTex);
                _hueRingTex = null;
            }
            if (_triangleTex != null)
            {
                UnityEngine.Object.DestroyImmediate(_triangleTex);
                _triangleTex = null;
            }
            _triangleBarycentricCache = null;
            _cachedBarycentricSize = 0;
            _cachedTriangleHue = -1f;
        }

        public static Color HSLColorField(string label, Color color, float size = 220f)
        {
            if (!string.IsNullOrEmpty(label))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            }
            Rect pickerRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            Color updated = Draw(pickerRect, color);
            updated = DrawNumericInputs(updated);
            return updated;
        }

        public static Color Draw(Rect rect, Color color)
        {
            float h, s, l;
            RGBToHSL(color, out h, out s, out l);

            EnsureRingTexture();
            EnsureTriangleTexture(h);

            float size = Mathf.Min(rect.width, rect.height);
            Rect ringRect = new Rect(
                rect.x + (rect.width - size) * 0.5f,
                rect.y + (rect.height - size) * 0.5f,
                size, size);
            Vector2 center = ringRect.center;
            float outerR = size * 0.5f;
            float innerR = outerR * (1f - RingThicknessRatio);
            float triR = innerR * TriangleInsetRatio;

            Vector2 vTop, vLeft, vRight;
            GetTriangleVertices(center, triR, h, out vTop, out vLeft, out vRight);

            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;

            switch (e.GetTypeForControl(controlID))
            {
                case EventType.Repaint:
                {
                    GUI.DrawTexture(ringRect, _hueRingTex);

                    float triBoxSize = triR * 2f;
                    Rect triBoxRect = new Rect(center.x - triR, center.y - triR, triBoxSize, triBoxSize);
                    // Rotate the triangle texture so its pure-hue vertex aligns with
                    // the hue indicator on the ring. The texture content already encodes
                    // the gradient with the pure hue at "natural top" (high tex y).
                    Matrix4x4 prevMatrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(h * 360f, center);
                    GUI.DrawTexture(triBoxRect, _triangleTex);
                    GUI.matrix = prevMatrix;

                    DrawHueIndicator(center, h, innerR, outerR);
                    DrawTriangleIndicator(color, h, vTop, vLeft, vRight);
                    break;
                }

                case EventType.MouseDown:
                {
                    if (e.button == 0 && ringRect.Contains(e.mousePosition))
                    {
                        Vector2 local = e.mousePosition - center;
                        float dist = local.magnitude;
                        if (dist <= outerR && dist >= innerR)
                        {
                            GUIUtility.hotControl = controlID;
                            _dragMode = DragMode.Ring;
                            h = AngleToHue(local);
                            color = HSLToRGB(h, s, l);
                            GUI.changed = true;
                            e.Use();
                        }
                        else if (PointInTriangle(e.mousePosition, vTop, vLeft, vRight))
                        {
                            GUIUtility.hotControl = controlID;
                            _dragMode = DragMode.Triangle;
                            color = ColorFromTrianglePoint(e.mousePosition, vTop, vLeft, vRight, h);
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                    break;
                }

                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl == controlID)
                    {
                        if (_dragMode == DragMode.Ring)
                        {
                            Vector2 local = e.mousePosition - center;
                            h = AngleToHue(local);
                            color = HSLToRGB(h, s, l);
                            GUI.changed = true;
                            e.Use();
                        }
                        else if (_dragMode == DragMode.Triangle)
                        {
                            Vector2 clamped = ClampPointToTriangle(e.mousePosition, vTop, vLeft, vRight);
                            color = ColorFromTrianglePoint(clamped, vTop, vLeft, vRight, h);
                            GUI.changed = true;
                            e.Use();
                        }
                    }
                    break;
                }

                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        _dragMode = DragMode.None;
                        e.Use();
                    }
                    break;
                }
            }

            return color;
        }

        private static Color DrawNumericInputs(Color color)
        {
            float h, s, l;
            RGBToHSL(color, out h, out s, out l);
            EditorGUI.BeginChangeCheck();
            float newH = EditorGUILayout.Slider("Hue", h * 360f, 0f, 360f) / 360f;
            float newS = EditorGUILayout.Slider("Saturation", s, 0f, 1f);
            float newL = EditorGUILayout.Slider("Lightness", l, 0f, 1f);
            Color rgbField = EditorGUILayout.ColorField("RGB", color);
            if (EditorGUI.EndChangeCheck())
            {
                if (!Mathf.Approximately(rgbField.r, color.r) ||
                    !Mathf.Approximately(rgbField.g, color.g) ||
                    !Mathf.Approximately(rgbField.b, color.b) ||
                    !Mathf.Approximately(rgbField.a, color.a))
                {
                    return rgbField;
                }
                return HSLToRGB(Mathf.Repeat(newH, 1f), Mathf.Clamp01(newS), Mathf.Clamp01(newL));
            }
            return color;
        }

        private static void GetTriangleVertices(Vector2 center, float radius, float hue,
            out Vector2 top, out Vector2 left, out Vector2 right)
        {
            float baseAngle = hue * Mathf.PI * 2f;
            float step = Mathf.PI * 2f / 3f;
            top = VertexAt(center, radius, baseAngle);
            left = VertexAt(center, radius, baseAngle - step);
            right = VertexAt(center, radius, baseAngle + step);
        }

        private static Vector2 VertexAt(Vector2 center, float radius, float angle)
        {
            return center + new Vector2(Mathf.Sin(angle) * radius, -Mathf.Cos(angle) * radius);
        }

        private static void DrawHueIndicator(Vector2 center, float hue, float innerR, float outerR)
        {
            float midR = (innerR + outerR) * 0.5f;
            float angle = hue * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
            Vector2 pos = center + dir * midR;
            float marker = Mathf.Max(8f, (outerR - innerR) * 0.7f);
            DrawMarker(pos, marker);
        }

        private static void DrawTriangleIndicator(Color color, float hue, Vector2 vTop, Vector2 vLeft, Vector2 vRight)
        {
            float a, b, c;
            BarycentricFromColor(color, hue, out a, out b, out c);
            Vector2 pos = a * vTop + b * vLeft + c * vRight;
            DrawMarker(pos, 9f);
        }

        private static void DrawMarker(Vector2 pos, float size)
        {
            float half = size * 0.5f;
            Rect outer = new Rect(pos.x - half, pos.y - half, size, size);
            // Black border
            EditorGUI.DrawRect(new Rect(outer.x, outer.y, outer.width, 1f), Color.black);
            EditorGUI.DrawRect(new Rect(outer.x, outer.yMax - 1f, outer.width, 1f), Color.black);
            EditorGUI.DrawRect(new Rect(outer.x, outer.y, 1f, outer.height), Color.black);
            EditorGUI.DrawRect(new Rect(outer.xMax - 1f, outer.y, 1f, outer.height), Color.black);
            // White inset
            EditorGUI.DrawRect(new Rect(outer.x + 1f, outer.y + 1f, outer.width - 2f, 1f), Color.white);
            EditorGUI.DrawRect(new Rect(outer.x + 1f, outer.yMax - 2f, outer.width - 2f, 1f), Color.white);
            EditorGUI.DrawRect(new Rect(outer.x + 1f, outer.y + 1f, 1f, outer.height - 2f), Color.white);
            EditorGUI.DrawRect(new Rect(outer.xMax - 2f, outer.y + 1f, 1f, outer.height - 2f), Color.white);
        }

        private static float AngleToHue(Vector2 local)
        {
            float angle = Mathf.Atan2(local.x, -local.y);
            float hue = angle / (Mathf.PI * 2f);
            return Mathf.Repeat(hue, 1f);
        }

        private static Color ColorFromTrianglePoint(Vector2 p, Vector2 vTop, Vector2 vLeft, Vector2 vRight, float hue)
        {
            float a, b, c;
            Barycentric(p, vTop, vLeft, vRight, out a, out b, out c);
            a = Mathf.Clamp01(a);
            b = Mathf.Clamp01(b);
            c = Mathf.Clamp01(c);
            float sum = a + b + c;
            if (sum > 0f) { a /= sum; b /= sum; c /= sum; }
            Color pure = HSLToRGB(hue, 1f, 0.5f);
            return new Color(
                a * pure.r + c,
                a * pure.g + c,
                a * pure.b + c,
                1f);
        }

        private static void BarycentricFromColor(Color color, float hue, out float a, out float b, out float c)
        {
            float cmax = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float cmin = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            a = Mathf.Clamp01(cmax - cmin);
            c = Mathf.Clamp01(cmin);
            b = Mathf.Clamp01(1f - cmax);
            float sum = a + b + c;
            if (sum > 0f) { a /= sum; b /= sum; c /= sum; }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static Vector2 ClampPointToTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            if (PointInTriangle(p, a, b, c)) return p;
            Vector2 p1 = ClosestPointOnSegment(p, a, b);
            Vector2 p2 = ClosestPointOnSegment(p, b, c);
            Vector2 p3 = ClosestPointOnSegment(p, c, a);
            float d1 = (p - p1).sqrMagnitude;
            float d2 = (p - p2).sqrMagnitude;
            float d3 = (p - p3).sqrMagnitude;
            if (d1 <= d2 && d1 <= d3) return p1;
            if (d2 <= d3) return p2;
            return p3;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return a + ab * t;
        }

        private static void Barycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c,
            out float u, out float v, out float w)
        {
            Vector2 v0 = b - a;
            Vector2 v1 = c - a;
            Vector2 v2 = p - a;
            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 1e-6f) { u = 1f; v = 0f; w = 0f; return; }
            v = (d11 * d20 - d01 * d21) / denom;
            w = (d00 * d21 - d01 * d20) / denom;
            u = 1f - v - w;
        }

        private static void EnsureRingTexture()
        {
            if (_hueRingTex != null) return;
            int size = RingTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = UnityEngine.FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;
            float outerR = size * 0.5f;
            float innerR = outerR * (1f - RingThicknessRatio);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > outerR || d < innerR)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    // y axis flipped to match screen (Unity texture origin at bottom-left)
                    float hue = AngleToHue(new Vector2(dx, -dy));
                    Color rgb = HSLToRGB(hue, 1f, 0.5f);
                    float aa = 1f;
                    if (d < innerR + 1f) aa = d - innerR;
                    else if (d > outerR - 1f) aa = outerR - d;
                    aa = Mathf.Clamp01(aa);
                    pixels[y * size + x] = new Color(rgb.r, rgb.g, rgb.b, aa);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            _hueRingTex = tex;
        }

        private static void EnsureTriangleTexture(float hue)
        {
            if (_triangleTex != null && Mathf.Abs(hue - _cachedTriangleHue) < 1e-3f) return;

            int size = TriangleTextureSize;
            if (_triangleTex == null)
            {
                _triangleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                _triangleTex.hideFlags = HideFlags.HideAndDontSave;
                _triangleTex.filterMode = UnityEngine.FilterMode.Bilinear;
                _triangleTex.wrapMode = TextureWrapMode.Clamp;
            }

            EnsureTriangleBarycentricCache(size);

            // Per-hue work is just a linear blend over the precomputed weights:
            //   color = a * pureHue + c * white   (b weight contributes black)
            Color pure = HSLToRGB(hue, 1f, 0.5f);
            int total = size * size;
            var pixels = new Color32[total];
            for (int i = 0; i < total; i++)
            {
                Vector3 w = _triangleBarycentricCache[i];
                if (w.x < 0f)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }
                float a = w.x;
                float c = w.z;
                pixels[i] = new Color(a * pure.r + c, a * pure.g + c, a * pure.b + c, 1f);
            }
            _triangleTex.SetPixels32(pixels);
            _triangleTex.Apply(false);
            _cachedTriangleHue = hue;
        }

        private static void EnsureTriangleBarycentricCache(int size)
        {
            if (_triangleBarycentricCache != null && _cachedBarycentricSize == size) return;

            // Triangle is computed in texture coordinates (y up). Pure hue vertex is
            // at high y so it renders at the top of the rect on screen.
            float R = size * 0.5f - 0.5f;
            float cx = size * 0.5f;
            float cy = size * 0.5f;
            Vector2 vTop = new Vector2(cx, cy + R);
            float ang = Mathf.PI * 2f / 3f;
            Vector2 vLeft = new Vector2(cx + Mathf.Sin(-ang) * R, cy + Mathf.Cos(-ang) * R);
            Vector2 vRight = new Vector2(cx + Mathf.Sin(ang) * R, cy + Mathf.Cos(ang) * R);

            var cache = new Vector3[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float a, b, c;
                    Barycentric(p, vTop, vLeft, vRight, out a, out b, out c);
                    if (a < -0.005f || b < -0.005f || c < -0.005f)
                    {
                        // Sentinel: x < 0 marks "outside the triangle".
                        cache[y * size + x] = new Vector3(-1f, 0f, 0f);
                        continue;
                    }
                    cache[y * size + x] = new Vector3(
                        Mathf.Clamp01(a),
                        Mathf.Clamp01(b),
                        Mathf.Clamp01(c));
                }
            }
            _triangleBarycentricCache = cache;
            _cachedBarycentricSize = size;
        }

        public static void RGBToHSL(Color color, out float h, out float s, out float l)
        {
            float r = color.r, g = color.g, b = color.b;
            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            l = (max + min) * 0.5f;
            float d = max - min;
            if (d < 1e-6f)
            {
                h = 0f;
                s = 0f;
                return;
            }
            s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6f : 0f);
            else if (max == g) h = (b - r) / d + 2f;
            else h = (r - g) / d + 4f;
            h /= 6f;
        }

        public static Color HSLToRGB(float h, float s, float l)
        {
            h = Mathf.Repeat(h, 1f);
            s = Mathf.Clamp01(s);
            l = Mathf.Clamp01(l);
            if (s < 1e-6f)
            {
                return new Color(l, l, l, 1f);
            }
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            float r = HueToChannel(p, q, h + 1f / 3f);
            float g = HueToChannel(p, q, h);
            float b = HueToChannel(p, q, h - 1f / 3f);
            return new Color(r, g, b, 1f);
        }

        private static float HueToChannel(float p, float q, float t)
        {
            t = Mathf.Repeat(t, 1f);
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }
}
