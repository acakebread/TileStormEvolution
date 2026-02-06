using UnityEngine;

namespace MassiveHadronLtd
{
    public static class ScreenSpaceUtil
    {
        private static RenderTexture _rt;
        private static Mesh _gridMesh;
        private static Material _quadMaterial;
        private static Texture2D _xorTexture;

        private static int _lastColumns = -1;
        private static int _lastRows = -1;
        private static Vector2 _lastCoord = new Vector2(-999f, -999f);

        public static void SetTexture(Texture2D value) => _xorTexture = value;

		private static void LazyInitResources()
        {
            if (_quadMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default") 
                             ?? Shader.Find("Hidden/Internal-Colored") 
                             ?? Shader.Find("Particles/Standard Unlit") 
                             ?? Shader.Find("Universal Render Pipeline/Unlit");

                if (shader == null)
                {
                    Debug.LogError("[ScreenSpaceUtil] No usable shader found!");
                    return;
                }

                _quadMaterial = new Material(shader);
            }

            if (_xorTexture == null)
            {
                //_xorTexture = TextureUtils.GenerateCheckerTexture(Screen.width / 8, Screen.height / 8);
				_xorTexture = TextureUtils.GeneratePerlinNoiseTexture();
				if (_xorTexture != null) _xorTexture.filterMode = FilterMode.Point;
            }
        }

        private static void RebuildGridMeshIfNeeded(int numColumns, int numRows, Vector2 center)
        {
            if (numColumns < 1) numColumns = 1;
            if (numRows < 1) numRows = 1;

            bool needsRebuild = _gridMesh == null ||
                                _lastColumns != numColumns ||
                                _lastRows != numRows ||
                                Vector2.Distance(_lastCoord, center) > 0.0005f;

            if (!needsRebuild) return;

            if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);

            _gridMesh = new Mesh { name = $"DeformGrid_{numColumns}x{numRows}" };

            int totalCells = numColumns * numRows;
            int totalVerts = totalCells * 4;
            int totalIndices = totalCells * 6;

            Vector3[] vertices = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];
            Color[] colors = new Color[totalVerts];
            int[] indices = new int[totalIndices];

            float cellWidth = 1f;
            float cellHeight = 1f;
            float uvScaleX = 1f / numColumns;
            float uvScaleY = 1f / numRows;

            var centerLogical = new Vector2(center.x / uvScaleX, center.y / uvScaleY);
			float falloffRadius = 0.35f / uvScaleY;
			float maxDisplacement = 1.3f;

			for (int qy = 0; qy < numRows; qy++)
            {
                for (int qx = 0; qx < numColumns; qx++)
                {
                    float x0 = qx * cellWidth;
                    float y0 = qy * cellHeight;
                    float x1 = x0 + cellWidth;
                    float y1 = y0 + cellHeight;

                    Vector2[] corners = { new(x0, y0), new(x0, y1), new(x1, y1), new(x1, y0) };

					var d0 = (new Vector2(x0, y0) - centerLogical).sqrMagnitude;
					var d1 = (new Vector2(x0, y1) - centerLogical).sqrMagnitude;
					var d2 = (new Vector2(x1, y1) - centerLogical).sqrMagnitude;
					var d3 = (new Vector2(x1, y0) - centerLogical).sqrMagnitude;

					Vector2 quadCenter = new ( (x0 + x1) * 0.5f, (y0 + y1) * 0.5f );
					Vector2 centreDelta = quadCenter - centerLogical;
					float centreDist = centreDelta.magnitude;
					Vector2 centreDir = centreDist > 1e-6f ? centreDelta.normalized : Vector2.zero;

					var dt = (d0 + d1 + d2 + d3) * 0.25f;
                    var sqrRadius = falloffRadius * falloffRadius;
					var strength = dt < sqrRadius ? 1f - dt / sqrRadius : 0f;

                    var trans = centreDir * strength * maxDisplacement * 0f;
                    var scaleX = 1f + strength * maxDisplacement;
                    var scaleY = 1f + strength * maxDisplacement;

					int baseVert = (qy * numColumns + qx) * 4;

                    for (int i = 0; i < 4; i++)
                    {
						var pos = corners[i];
						var delta = pos - centerLogical;
                        vertices[baseVert] = centerLogical + trans + new Vector2(delta.x * scaleX, delta.y * scaleY);

						uvs[baseVert] = new Vector2(pos.x * uvScaleX, pos.y * uvScaleY);
                        colors[baseVert] = Color.white;
						baseVert++;
                    }
                }
            }

            // ────────── Phase 2: Snap quads to squares ──────────
            for (int qy = 0; qy < numRows; qy++)
            {
                for (int qx = 0; qx < numColumns; qx++)
                {
					int vertIdx = (qy * numColumns + qx) * 4;

					Vector3 bl = vertices[vertIdx + 0];
                    Vector3 tl = vertices[vertIdx + 1];
                    Vector3 tr = vertices[vertIdx + 2];
                    Vector3 br = vertices[vertIdx + 3];

                    float left = Mathf.Min(bl.x, tl.x);
                    float right = Mathf.Max(tr.x, br.x);
                    float bottom = Mathf.Min(bl.y, br.y);
                    float top = Mathf.Max(tl.y, tr.y);

                    float side = Mathf.Max(right - left, top - bottom);
                    float cx = (left + right) * 0.5f;
                    float cy = (bottom + top) * 0.5f;

                    left = cx - side * 0.5f;
                    right = cx + side * 0.5f;
                    bottom = cy - side * 0.5f;
                    top = cy + side * 0.5f;

                    vertices[vertIdx + 0] = new Vector3(left, bottom, 0f);
                    vertices[vertIdx + 1] = new Vector3(left, top, 0f);
                    vertices[vertIdx + 2] = new Vector3(right, top, 0f);
                    vertices[vertIdx + 3] = new Vector3(right, bottom, 0f);
                }
            }

            // ────────── Phase 3: Sort quads by size ascending ──────────
            var quads = new System.Collections.Generic.List<(float area, int baseVert)>(totalCells);
            for (int q = 0; q < totalCells; q++)
            {
                int baseVert = q * 4;
                Vector3 bl = vertices[baseVert + 0];
                Vector3 tr = vertices[baseVert + 2];
                float area = (tr.x - bl.x) * (tr.y - bl.y);
                quads.Add((area, baseVert));
            }
            quads.Sort((a, b) => a.area.CompareTo(b.area));

            Vector3[] sortedVerts = new Vector3[vertices.Length];
            Vector2[] sortedUVs = new Vector2[uvs.Length];
            Color[] sortedColors = new Color[colors.Length];
            int[] sortedIndices = new int[indices.Length];

            for (int q = 0; q < quads.Count; q++)
            {
                int oldBase = quads[q].baseVert;
                int newBase = q * 4;

                for (int i = 0; i < 4; i++)
                {
                    sortedVerts[newBase + i] = vertices[oldBase + i];
                    sortedUVs[newBase + i] = uvs[oldBase + i];
                    sortedColors[newBase + i] = colors[oldBase + i];
                }

                int idxOffset = q * 6;
                sortedIndices[idxOffset + 0] = newBase + 0;
                sortedIndices[idxOffset + 1] = newBase + 1;
                sortedIndices[idxOffset + 2] = newBase + 2;
                sortedIndices[idxOffset + 3] = newBase + 0;
                sortedIndices[idxOffset + 4] = newBase + 2;
                sortedIndices[idxOffset + 5] = newBase + 3;
            }

            // ────────── Phase 4: Project vertices to final UV space ──────────
            for (int n = 0; n < sortedVerts.Length; n++)
            {
                sortedVerts[n] = new Vector3(sortedVerts[n].x * uvScaleX, sortedVerts[n].y * uvScaleY, sortedVerts[n].z);
            }

            _gridMesh.vertices = sortedVerts;
            _gridMesh.uv = sortedUVs;
            _gridMesh.colors = sortedColors;
            _gridMesh.triangles = sortedIndices;

            _lastColumns = numColumns;
            _lastRows = numRows;
            _lastCoord = center;
        }

        private static void DrawGridToRT(int numColumns, int numRows, Vector2 coord)
        {
            LazyInitResources();

            int pw = numColumns * 64;
            int ph = numRows * 64;

            if (_rt == null || _rt.width != pw || _rt.height != ph || !_rt.IsCreated())
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(pw, ph, 0, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                    antiAliasing = 1
                };
                _rt.Create();
            }

            if (_xorTexture == null || _quadMaterial == null) return;

            RebuildGridMeshIfNeeded(numColumns, numRows, coord);

            var oldRT = RenderTexture.active;
            RenderTexture.active = _rt;

			GL.Clear(true, true, Color.darkSlateGray);//GL.Clear(true, true, Color.clear);//may need clear
			GL.PushMatrix();
            GL.LoadOrtho();

            _quadMaterial.mainTexture = _xorTexture;
            _quadMaterial.color = Color.white;
            _quadMaterial.SetPass(0);

            Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);

            GL.PopMatrix();
            RenderTexture.active = oldRT;
        }

        public static RenderTexture GetRenderTexture(int numColumns = 8, int numRows = 8, Vector2 coord = default)
        {
            if (coord == default) coord = new Vector2(0.5f, 0.5f);
            DrawGridToRT(numColumns, numRows, coord);
            return _rt;
        }

        public static void Cleanup()
        {
            if (_rt != null) { _rt.Release(); _rt = null; }
            if (_gridMesh != null) { Object.DestroyImmediate(_gridMesh); _gridMesh = null; }
            if (_quadMaterial != null) { Object.DestroyImmediate(_quadMaterial); _quadMaterial = null; }
            _xorTexture = null;
            _lastColumns = _lastRows = -1;
            _lastCoord = new Vector2(-999f, -999f);
        }
    }
}
