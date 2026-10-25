using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuticaLab.TextureCocktail
{
    /// <summary>
    /// Owns polygon interaction state and the generated mask texture. Points use normalized
    /// texture coordinates (origin bottom-left); GUI conversion flips Y at the boundary.
    /// Completed edits notify the owner once. Dragging retains the original mouse-up render policy.
    /// The owner borrows MaskTexture and must not destroy it.
    /// </summary>
    internal sealed class PolygonMaskEditor : IDisposable
    {
        private class PolygonMaskShape
        {
            public List<Vector2> Points = new List<Vector2>();
            public bool Closed = false;
        }

        private enum PolygonInteractionMode { None, DraggingVertex, DraggingShape }

        private readonly List<PolygonMaskShape> _polygonMaskShapes = new List<PolygonMaskShape>();
        private bool _polygonMaskEnabled = true;
        private PolygonInteractionMode _polygonInteractionMode = PolygonInteractionMode.None;
        private int _draggingPolygonIndex = -1;
        private int _draggingPolygonPoint = -1;
        private Vector2 _polygonDragPreviousMaskPos;
        private bool _polygonMaskDirty = false;
        private Texture2D _polygonMaskTexture;
        private const float _polygonVertexHitDistance = 10f;
        private Color32[] _maskPixels;

        private readonly Action _onChanged;

        internal PolygonMaskEditor(Action onChanged) { _onChanged = onChanged; }
        internal Texture2D MaskTexture => _polygonMaskTexture;
        internal bool HasClosedShapes => HasAnyClosedPolygon();
        internal int ShapeCount => _polygonMaskShapes.Count;
        internal bool Enabled
        {
            get => _polygonMaskEnabled;
            set
            {
                if (_polygonMaskEnabled == value) return;
                _polygonMaskEnabled = value;
                NotifyChanged();
            }
        }

        private void NotifyChanged() => _onChanged?.Invoke();

        internal Texture2D GetTexture(Texture2D source)
        {
            UpdatePolygonMaskTexture(source);
            return _polygonMaskTexture;
        }

        internal void GetPolygonShapeCounts(out int closedCount, out int openCount)
        {
            closedCount = 0;
            openCount = 0;
            for (int i = 0; i < _polygonMaskShapes.Count; i++)
            {
                if (_polygonMaskShapes[i].Closed) closedCount++; else openCount++;
            }
        }

        internal Rect GetImageDrawRect(Rect previewRect, int textureWidth, int textureHeight)
        {
            float scaleX = previewRect.width / textureWidth;
            float scaleY = previewRect.height / textureHeight;
            float scale = Mathf.Min(scaleX, scaleY);
            float scaledWidth = textureWidth * scale;
            float scaledHeight = textureHeight * scale;
            float x = previewRect.x + (previewRect.width - scaledWidth) * 0.5f;
            float y = previewRect.y + (previewRect.height - scaledHeight) * 0.5f;
            return new Rect(x, y, scaledWidth, scaledHeight);
        }

        internal void DrawPolygonMaskOverlay(Rect imageDrawRect)
        {
            if (_polygonMaskShapes.Count == 0)
            {
                return;
            }

            Handles.BeginGUI();
            Color previousColor = Handles.color;
            Color closedColor = _polygonMaskEnabled ? new Color(0.1f, 1f, 0.4f, 0.95f) : new Color(0.8f, 0.8f, 0.8f, 0.85f);
            Color openColor = _polygonMaskEnabled ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(0.7f, 0.7f, 0.7f, 0.75f);
            Color activeShapeColor = _polygonMaskEnabled ? new Color(0.2f, 0.7f, 1f, 1f) : closedColor;

            for (int shapeIndex = 0; shapeIndex < _polygonMaskShapes.Count; shapeIndex++)
            {
                var shape = _polygonMaskShapes[shapeIndex];
                if (shape.Points.Count == 0)
                {
                    continue;
                }
                bool isActiveDragShape = _polygonInteractionMode == PolygonInteractionMode.DraggingShape && _draggingPolygonIndex == shapeIndex;
                Handles.color = isActiveDragShape ? activeShapeColor : (shape.Closed ? closedColor : openColor);

                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 currentPoint = MaskPointToGUI(shape.Points[i], imageDrawRect);
                    Vector2 nextPoint;
                    if (i == shape.Points.Count - 1)
                    {
                        if (!shape.Closed)
                        {
                            continue;
                        }
                        nextPoint = MaskPointToGUI(shape.Points[0], imageDrawRect);
                    }
                    else
                    {
                        nextPoint = MaskPointToGUI(shape.Points[i + 1], imageDrawRect);
                    }
                    Handles.DrawAAPolyLine(2.0f, currentPoint, nextPoint);
                }

                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 point = MaskPointToGUI(shape.Points[i], imageDrawRect);
                    float size = i == 0 && !shape.Closed ? 5f : 4f;
                    Handles.DrawSolidDisc(point, Vector3.forward, size);
                }
            }

            Handles.color = previousColor;
            Handles.EndGUI();
        }

        internal void HandlePolygonMaskInteraction(Rect imageDrawRect, Event currentEvent)
        {
            if (!_polygonMaskEnabled)
            {
                ResetPolygonInteractionState();
                return;
            }

            if (_polygonInteractionMode != PolygonInteractionMode.None)
            {
                if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
                {
                    ContinuePolygonDrag(currentEvent.mousePosition, imageDrawRect);
                    currentEvent.Use();
                    return;
                }
                if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
                {
                    if (_polygonMaskDirty)
                    {
                        NotifyChanged();
                    }
                    ResetPolygonInteractionState();
                    currentEvent.Use();
                    return;
                }
            }

            if (!imageDrawRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
            {
                if (currentEvent.clickCount >= 2)
                {
                    if (DeletePolygonAtMouse(currentEvent.mousePosition, imageDrawRect))
                    {
                        NotifyChanged();
                    }
                    else
                    {
                        ClearPolygonMask();
                    }
                    currentEvent.Use();
                    return;
                }
                int openIndexRC = GetOpenPolygonIndex();
                if (openIndexRC >= 0)
                {
                    var openShapeRC = _polygonMaskShapes[openIndexRC];
                    if (openShapeRC.Points.Count > 0)
                    {
                        openShapeRC.Points.RemoveAt(openShapeRC.Points.Count - 1);
                    }
                    if (openShapeRC.Points.Count == 0)
                    {
                        _polygonMaskShapes.RemoveAt(openIndexRC);
                    }
                    NotifyChanged();
                }
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                if (currentEvent.clickCount >= 2)
                {
                    int dummyShape, dummyVertex;
                    bool nearVertex = TryGetNearestPolygonVertex(currentEvent.mousePosition, imageDrawRect, _polygonVertexHitDistance, out dummyShape, out dummyVertex);
                    if (!nearVertex)
                    {
                        int edgeShape, edgeStart;
                        if (TryGetNearestPolygonEdge(currentEvent.mousePosition, imageDrawRect, _polygonVertexHitDistance, true, out edgeShape, out edgeStart))
                        {
                            var edgeShapeRef = _polygonMaskShapes[edgeShape];
                            int nextIdx = (edgeStart + 1) % edgeShapeRef.Points.Count;
                            Vector2 midpoint = (edgeShapeRef.Points[edgeStart] + edgeShapeRef.Points[nextIdx]) * 0.5f;
                            edgeShapeRef.Points.Insert(edgeStart + 1, midpoint);
                            NotifyChanged();
                            currentEvent.Use();
                            return;
                        }
                    }
                }

                int hitShapeIndex, hitVertexIndex;
                if (TryGetNearestPolygonVertex(currentEvent.mousePosition, imageDrawRect, _polygonVertexHitDistance, out hitShapeIndex, out hitVertexIndex))
                {
                    var hitShape = _polygonMaskShapes[hitShapeIndex];
                    if (!hitShape.Closed && hitVertexIndex == 0 && hitShape.Points.Count >= 3)
                    {
                        hitShape.Closed = true;
                        NotifyChanged();
                        currentEvent.Use();
                        return;
                    }
                    if (hitShape.Closed)
                    {
                        _polygonInteractionMode = PolygonInteractionMode.DraggingVertex;
                        _draggingPolygonIndex = hitShapeIndex;
                        _draggingPolygonPoint = hitVertexIndex;
                        currentEvent.Use();
                        return;
                    }
                }

                int openIndex = GetOpenPolygonIndex();
                if (openIndex >= 0)
                {
                    var openShape = _polygonMaskShapes[openIndex];
                    if (openShape.Points.Count >= 3)
                    {
                        Vector2 lastPointGui = MaskPointToGUI(openShape.Points[openShape.Points.Count - 1], imageDrawRect);
                        if (Vector2.Distance(lastPointGui, currentEvent.mousePosition) <= _polygonVertexHitDistance)
                        {
                            openShape.Closed = true;
                            NotifyChanged();
                            currentEvent.Use();
                            return;
                        }
                    }

                    int splitShape, splitEdgeStart;
                    if (TryGetNearestPolygonEdge(currentEvent.mousePosition, imageDrawRect, _polygonVertexHitDistance, false, out splitShape, out splitEdgeStart))
                    {
                        var matchedShape = _polygonMaskShapes[splitShape];
                        int next = (splitEdgeStart + 1) % matchedShape.Points.Count;
                        Vector2 a = MaskPointToGUI(matchedShape.Points[splitEdgeStart], imageDrawRect);
                        Vector2 b = MaskPointToGUI(matchedShape.Points[next], imageDrawRect);
                        Vector2 projected = ProjectPointOnSegment(currentEvent.mousePosition, a, b);
                        Vector2 projectedMaskPos = MouseToMaskPoint(projected, imageDrawRect);
                        matchedShape.Points.Insert(splitEdgeStart + 1, projectedMaskPos);
                        if (openShape.Points.Count <= 1)
                        {
                            _polygonMaskShapes.Remove(openShape);
                        }
                        NotifyChanged();
                        currentEvent.Use();
                        return;
                    }

                    openShape.Points.Add(MouseToMaskPoint(currentEvent.mousePosition, imageDrawRect));
                    NotifyChanged();
                    currentEvent.Use();
                    return;
                }

                Vector2 maskPos = MouseToMaskPoint(currentEvent.mousePosition, imageDrawRect);
                int containingIndex = FindClosedPolygonContainingMaskPoint(maskPos);
                if (containingIndex >= 0 && !currentEvent.shift)
                {
                    _polygonInteractionMode = PolygonInteractionMode.DraggingShape;
                    _draggingPolygonIndex = containingIndex;
                    _polygonDragPreviousMaskPos = maskPos;
                    currentEvent.Use();
                    return;
                }

                var newShape = new PolygonMaskShape();
                newShape.Points.Add(maskPos);
                _polygonMaskShapes.Add(newShape);
                NotifyChanged();
                currentEvent.Use();
                return;
            }
        }

        private void ContinuePolygonDrag(Vector2 mousePosition, Rect imageDrawRect)
        {
            if (_draggingPolygonIndex < 0 || _draggingPolygonIndex >= _polygonMaskShapes.Count)
            {
                ResetPolygonInteractionState();
                return;
            }
            var shape = _polygonMaskShapes[_draggingPolygonIndex];
            Vector2 currentMaskPos = MouseToMaskPoint(mousePosition, imageDrawRect);

            if (_polygonInteractionMode == PolygonInteractionMode.DraggingVertex)
            {
                if (_draggingPolygonPoint < 0 || _draggingPolygonPoint >= shape.Points.Count)
                {
                    ResetPolygonInteractionState();
                    return;
                }
                shape.Points[_draggingPolygonPoint] = currentMaskPos;
                _polygonMaskDirty = true;
                return;
            }

            if (_polygonInteractionMode == PolygonInteractionMode.DraggingShape)
            {
                if (shape.Points.Count == 0)
                {
                    ResetPolygonInteractionState();
                    return;
                }
                Vector2 delta = currentMaskPos - _polygonDragPreviousMaskPos;
                float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 p = shape.Points[i];
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.y > maxY) maxY = p.y;
                }
                float dx = Mathf.Clamp(delta.x, -minX, 1f - maxX);
                float dy = Mathf.Clamp(delta.y, -minY, 1f - maxY);
                if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
                {
                    return;
                }
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 p = shape.Points[i];
                    shape.Points[i] = new Vector2(p.x + dx, p.y + dy);
                }
                _polygonDragPreviousMaskPos = new Vector2(_polygonDragPreviousMaskPos.x + dx, _polygonDragPreviousMaskPos.y + dy);
                _polygonMaskDirty = true;
            }
        }

        private void ResetPolygonInteractionState()
        {
            _polygonInteractionMode = PolygonInteractionMode.None;
            _draggingPolygonIndex = -1;
            _draggingPolygonPoint = -1;
            _polygonMaskDirty = false;
        }

        private bool TryGetNearestPolygonVertex(Vector2 mousePosition, Rect imageDrawRect, float threshold, out int shapeIndex, out int vertexIndex)
        {
            float bestDistance = threshold;
            shapeIndex = -1;
            vertexIndex = -1;
            for (int s = 0; s < _polygonMaskShapes.Count; s++)
            {
                var shape = _polygonMaskShapes[s];
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Vector2 pointGui = MaskPointToGUI(shape.Points[i], imageDrawRect);
                    float distance = Vector2.Distance(pointGui, mousePosition);
                    if (distance <= bestDistance)
                    {
                        bestDistance = distance;
                        shapeIndex = s;
                        vertexIndex = i;
                    }
                }
            }
            return shapeIndex >= 0;
        }

        private bool TryGetNearestPolygonEdge(Vector2 mousePosition, Rect imageDrawRect, float threshold, bool closedOnly, out int shapeIndex, out int edgeStartIndex)
        {
            float bestDistance = threshold;
            shapeIndex = -1;
            edgeStartIndex = -1;
            for (int s = 0; s < _polygonMaskShapes.Count; s++)
            {
                var shape = _polygonMaskShapes[s];
                if (closedOnly && !shape.Closed) continue;
                if (shape.Points.Count < 2) continue;
                int edgeCount = shape.Closed ? shape.Points.Count : shape.Points.Count - 1;
                for (int i = 0; i < edgeCount; i++)
                {
                    int next = (i + 1) % shape.Points.Count;
                    Vector2 a = MaskPointToGUI(shape.Points[i], imageDrawRect);
                    Vector2 b = MaskPointToGUI(shape.Points[next], imageDrawRect);
                    float dist = DistancePointToSegment(mousePosition, a, b);
                    if (dist <= bestDistance)
                    {
                        bestDistance = dist;
                        shapeIndex = s;
                        edgeStartIndex = i;
                    }
                }
            }
            return shapeIndex >= 0;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
            Vector2 proj = a + ab * t;
            return Vector2.Distance(p, proj);
        }

        private static Vector2 ProjectPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 1e-6f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
            return a + ab * t;
        }

        private int FindClosedPolygonContainingMaskPoint(Vector2 maskPoint)
        {
            for (int s = _polygonMaskShapes.Count - 1; s >= 0; s--)
            {
                var shape = _polygonMaskShapes[s];
                if (!shape.Closed || shape.Points.Count < 3)
                {
                    continue;
                }
                if (IsPointInsidePolygon(maskPoint, shape.Points))
                {
                    return s;
                }
            }
            return -1;
        }

        private int GetOpenPolygonIndex()
        {
            for (int i = 0; i < _polygonMaskShapes.Count; i++)
            {
                if (!_polygonMaskShapes[i].Closed)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool DeletePolygonAtMouse(Vector2 mousePosition, Rect imageDrawRect)
        {
            int hitShapeIndex, hitVertexIndex;
            if (TryGetNearestPolygonVertex(mousePosition, imageDrawRect, _polygonVertexHitDistance, out hitShapeIndex, out hitVertexIndex))
            {
                _polygonMaskShapes.RemoveAt(hitShapeIndex);
                ResetPolygonInteractionState();
                return true;
            }
            Vector2 maskPos = MouseToMaskPoint(mousePosition, imageDrawRect);
            int containingIndex = FindClosedPolygonContainingMaskPoint(maskPos);
            if (containingIndex >= 0)
            {
                _polygonMaskShapes.RemoveAt(containingIndex);
                ResetPolygonInteractionState();
                return true;
            }
            return false;
        }

        internal bool FinalizeOpenPolygon()
        {
            int openIndex = GetOpenPolygonIndex();
            if (openIndex < 0)
            {
                return false;
            }
            var openShape = _polygonMaskShapes[openIndex];
            if (openShape.Points.Count >= 3)
            {
                openShape.Closed = true;
            }
            else
            {
                _polygonMaskShapes.RemoveAt(openIndex);
            }
            ResetPolygonInteractionState();
            NotifyChanged();
            return true;
        }

        private bool HasAnyClosedPolygon()
        {
            for (int i = 0; i < _polygonMaskShapes.Count; i++)
            {
                var shape = _polygonMaskShapes[i];
                if (shape.Closed && shape.Points.Count >= 3)
                {
                    return true;
                }
            }
            return false;
        }

        private Vector2 MouseToMaskPoint(Vector2 mousePosition, Rect imageDrawRect)
        {
            float normalizedX = Mathf.Clamp01((mousePosition.x - imageDrawRect.x) / imageDrawRect.width);
            float normalizedYFromTop = Mathf.Clamp01((mousePosition.y - imageDrawRect.y) / imageDrawRect.height);
            return new Vector2(normalizedX, 1f - normalizedYFromTop);
        }

        private Vector2 MaskPointToGUI(Vector2 normalizedPoint, Rect imageDrawRect)
        {
            float x = imageDrawRect.x + Mathf.Clamp01(normalizedPoint.x) * imageDrawRect.width;
            float y = imageDrawRect.y + (1f - Mathf.Clamp01(normalizedPoint.y)) * imageDrawRect.height;
            return new Vector2(x, y);
        }

        internal void ClearPolygonMask()
        {
            _polygonMaskShapes.Clear();
            ResetPolygonInteractionState();
            NotifyChanged();
        }

        private void UpdatePolygonMaskTexture(Texture2D source)
        {
            if (source == null || !HasAnyClosedPolygon())
            {
                return;
            }

            int width = source.width;
            int height = source.height;
            if (_polygonMaskTexture == null || _polygonMaskTexture.width != width || _polygonMaskTexture.height != height)
            {
                if (_polygonMaskTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(_polygonMaskTexture);
                }
                _polygonMaskTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _polygonMaskTexture.wrapMode = TextureWrapMode.Clamp;
                _polygonMaskTexture.filterMode = UnityEngine.FilterMode.Bilinear;
            }

            int totalPixels = width * height;
            if (_maskPixels == null || _maskPixels.Length != totalPixels)
            {
                _maskPixels = new Color32[totalPixels];
            }
            else
            {
                Array.Clear(_maskPixels, 0, totalPixels);
            }
            Color32[] pixels = _maskPixels;
            Color32 insideColor = new Color32(255, 255, 255, 255);

            for (int s = 0; s < _polygonMaskShapes.Count; s++)
            {
                var shape = _polygonMaskShapes[s];
                if (!shape.Closed || shape.Points.Count < 3)
                {
                    continue;
                }

                Vector2[] pixelPoints = new Vector2[shape.Points.Count];
                int minX = width;
                int maxX = 0;
                int minY = height;
                int maxY = 0;

                for (int i = 0; i < shape.Points.Count; i++)
                {
                    int px = Mathf.Clamp(Mathf.RoundToInt(shape.Points[i].x * (width - 1)), 0, width - 1);
                    int py = Mathf.Clamp(Mathf.RoundToInt(shape.Points[i].y * (height - 1)), 0, height - 1);
                    pixelPoints[i] = new Vector2(px, py);
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minY) minY = py;
                    if (py > maxY) maxY = py;
                }

                minX = Mathf.Clamp(minX, 0, width - 1);
                maxX = Mathf.Clamp(maxX, 0, width - 1);
                minY = Mathf.Clamp(minY, 0, height - 1);
                maxY = Mathf.Clamp(maxY, 0, height - 1);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int idx = y * width + x;
                        if (pixels[idx].a == 255)
                        {
                            continue;
                        }
                        if (IsPointInsidePolygon(new Vector2(x + 0.5f, y + 0.5f), pixelPoints))
                        {
                            pixels[idx] = insideColor;
                        }
                    }
                }
            }

            _polygonMaskTexture.SetPixels32(pixels);
            _polygonMaskTexture.Apply(false, false);
        }

        private bool IsPointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygonPoints)
        {
            bool isInside = false;
            int count = polygonPoints.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 pointI = polygonPoints[i];
                Vector2 pointJ = polygonPoints[j];
                float edgeDeltaY = pointJ.y - pointI.y;
                if (Mathf.Abs(edgeDeltaY) < Mathf.Epsilon)
                {
                    continue;
                }
                bool intersects = ((pointI.y > point.y) != (pointJ.y > point.y)) &&
                                  (point.x < (pointJ.x - pointI.x) * (point.y - pointI.y) / edgeDeltaY + pointI.x);
                if (intersects)
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }

        public void Dispose()
        {
            if (_polygonMaskTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_polygonMaskTexture);
                _polygonMaskTexture = null;
            }
            _maskPixels = null;
            ResetPolygonInteractionState();
        }
    }
}
