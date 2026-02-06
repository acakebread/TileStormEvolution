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
            float falloffRadius = 0.2f / uvScaleY;
            float maxDisplacement = 0.05f / uvScaleY;

            int vertIdx = 0;
            int indexIdx = 0;

            for (int qy = 0; qy < numRows; qy++)
            {
                for (int qx = 0; qx < numColumns; qx++)
                {
                    float x0 = qx * cellWidth;
                    float y0 = qy * cellHeight;
                    float x1 = x0 + cellWidth;
                    float y1 = y0 + cellHeight;

                    Vector2[] corners = { new(x0, y0), new(x0, y1), new(x1, y1), new(x1, y0) };

					Vector2 quadCenter = new ( (x0 + x1) * 0.5f, (y0 + y1) * 0.5f );
					Vector2 _delta = quadCenter - centerLogical;
					float _dist = _delta.magnitude;
					Vector2 _dir = _dist > 1e-6f ? _delta.normalized : Vector2.zero;
					float _strength = (_dist < falloffRadius) ? Mathf.Cos(Mathf.PI * 0.5f * (_dist / falloffRadius)) : 0f;

					int baseVert = vertIdx;

                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 pos = corners[i];
                        Vector2 delta = pos - centerLogical;
                        float dist = delta.magnitude;
                        Vector2 dir = dist > 1e-6f ? delta.normalized : Vector2.zero;
                        float strength = (dist < falloffRadius) ? Mathf.Cos(Mathf.PI * 0.5f * (dist / falloffRadius)) : 0f;

                        strength = (strength + _strength) * 0.5f;

						Vector2 offset = dir * (maxDisplacement * strength);
						//Vector2 offset = _dir * (maxDisplacement * _strength);

                        vertices[vertIdx] = new Vector3(pos.x + offset.x, pos.y + offset.y, 0f);
                        uvs[vertIdx] = new Vector2(pos.x * uvScaleX, pos.y * uvScaleY);
                        colors[vertIdx] = Color.white;
                        vertIdx++;
                    }

                    indices[indexIdx++] = baseVert + 0;
                    indices[indexIdx++] = baseVert + 1;
                    indices[indexIdx++] = baseVert + 2;
                    indices[indexIdx++] = baseVert + 0;
                    indices[indexIdx++] = baseVert + 2;
                    indices[indexIdx++] = baseVert + 3;
                }
            }

            // ────────── Phase 2: Snap quads to squares ──────────
            vertIdx = 0;
            for (int qy = 0; qy < numRows; qy++)
            {
                for (int qx = 0; qx < numColumns; qx++)
                {
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

                    vertIdx += 4;
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

            GL.Clear(true, true, Color.clear);
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


//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public static class ScreenSpaceUtil
//	{
//		private static RenderTexture _rt;
//		private static Mesh _gridMesh;
//		private static Material _quadMaterial;
//		private static Texture2D _xorTexture;

//		private static int _lastColumns = -1;
//		private static int _lastRows = -1;
//		private static Vector2 _lastCoord = new Vector2(-999f, -999f);

//		private static void LazyInitResources()
//		{
//			if (_quadMaterial == null)
//			{
//				// Use a shader that actually reads vertex colors
//				var shader = Shader.Find("Sprites/Default");

//				if (shader == null)
//				{
//					// Fallbacks if Sprites/Default is missing (rare, but...)
//					shader = Shader.Find("Hidden/Internal-Colored")
//						  ?? Shader.Find("Particles/Standard Unlit")
//						  ?? Shader.Find("Universal Render Pipeline/Unlit");

//					if (shader == null)
//					{
//						Debug.LogError("[ScreenSpaceUtil] No usable shader found! Check project has Sprites/Default or URP package.");
//						return;
//					}
//				}

//				_quadMaterial = new Material(shader);
//			}

//			if (_xorTexture == null)
//			{
//				//_xorTexture = TextureUtils.GenerateXorTexture256();
//				_xorTexture = TextureUtils.GenerateCheckerTexture(Screen.width / 8, Screen.height / 8);
//				if (_xorTexture != null)
//				{
//					_xorTexture.filterMode = FilterMode.Point;
//				}
//			}
//		}

//		private static void RebuildGridMeshIfNeeded(int numColumns, int numRows, Vector2 center)
//		{
//			if (numColumns < 1) numColumns = 1;
//			if (numRows < 1) numRows = 1;

//			bool needsRebuild = _gridMesh == null ||
//								_lastColumns != numColumns ||
//								_lastRows != numRows ||
//								Vector2.Distance(_lastCoord, center) > 0.0005f;

//			if (!needsRebuild) return;

//			if (_gridMesh != null)
//				Object.DestroyImmediate(_gridMesh);

//			_gridMesh = new Mesh { name = $"DeformGrid_{numColumns}x{numRows}" };

//			// ────────────────────────────────────────────────────────────────
//			//  Preparation – logical / normalised space (0..1)
//			// ────────────────────────────────────────────────────────────────

//			int totalCells = numColumns * numRows;
//			int totalVerts = totalCells * 4;
//			int totalIndices = totalCells * 6;

//			Vector3[] vertices = new Vector3[totalVerts];
//			Vector2[] uvs = new Vector2[totalVerts];
//			Color[] colors = new Color[totalVerts];
//			int[] indices = new int[totalIndices];

//			float cellWidth = 1f;
//			float cellHeight = 1f;
//			float uvScaleX = 1f / numColumns;
//			float uvScaleY = 1f / numRows;

//			// Center is given in final screen/UV space → convert to logical space
//			var centerLogical = new Vector2(center.x / uvScaleX,center.y / uvScaleY);

//			// Displacement parameters – also in logical space
//			//const float falloffRadius = 0.25f;
//			//const float maxDisplacement = 0.2f;

//			float falloffRadius = 0.35f / uvScaleY;   // taller aspect → larger logical radius
//			float maxDisplacement = 0.10f / uvScaleY;

//			// ────────────────────────────────────────────────────────────────
//			//  Phase 1: Generate displaced positions (still in logical 0..1 space)
//			// ────────────────────────────────────────────────────────────────

//			int vertIdx = 0;
//			int indexIdx = 0;

//			for (int qy = 0; qy < numRows; qy++)
//			{
//				for (int qx = 0; qx < numColumns; qx++)
//				{
//					var x0 = qx * cellWidth;
//					var y0 = qy * cellHeight;
//					var x1 = x0 + cellWidth;
//					var y1 = y0 + cellHeight;

//					Vector2[] corners = {
//						new (x0, y0),  // BL
//						new (x0, y1),  // TL
//						new (x1, y1),  // TR
//						new (x1, y0)   // BR
//					};

//					int baseVert = vertIdx;

//					for (int i = 0; i < 4; i++)
//					{
//						Vector2 pos = corners[i];

//						// Aspect-corrected distance (makes radial effect circular in screen space)
//						Vector2 delta = pos - centerLogical;

//						float dist = delta.magnitude;
//						Vector2 dirOriginal = dist > 1e-6f ? delta.normalized : Vector2.zero;

//						float strength = 0f;
//						if (dist < falloffRadius)
//						{
//							float t = dist / falloffRadius;
//							strength = Mathf.Cos(Mathf.PI * 0.5f * t); // 1.0 → 0.0
//						}

//						Vector2 offset = dirOriginal * (maxDisplacement * strength);

//						vertices[vertIdx] = new Vector3(pos.x + offset.x, pos.y + offset.y, 0f);
//						uvs[vertIdx] = new Vector2(pos.x * uvScaleX, pos.y * uvScaleY);
//						colors[vertIdx] = Color.white; // or re-enable checkerboard if desired

//						vertIdx++;
//					}

//					// Clockwise quad (two triangles)
//					indices[indexIdx++] = baseVert + 0;
//					indices[indexIdx++] = baseVert + 1;
//					indices[indexIdx++] = baseVert + 2;

//					indices[indexIdx++] = baseVert + 0;
//					indices[indexIdx++] = baseVert + 2;
//					indices[indexIdx++] = baseVert + 3;
//				}
//			}

//			// ────────────────────────────────────────────────────────────────
//			//  Phase 2: Per-quad snapping → force inward-aligned rectangles or squares
//			// ────────────────────────────────────────────────────────────────

//			vertIdx = 0;

//			for (int qy = 0; qy < numRows; qy++)
//			{
//				for (int qx = 0; qx < numColumns; qx++)
//				{
//					Vector3 bl = vertices[vertIdx + 0];
//					Vector3 tl = vertices[vertIdx + 1];
//					Vector3 tr = vertices[vertIdx + 2];
//					Vector3 br = vertices[vertIdx + 3];

//					// ── Horizontal snapping ─────────────────────────────────
//					bool leftOfCenter = bl.x < centerLogical.x;
//					bool rightOfCenter = tr.x > centerLogical.x;

//					float leftX = leftOfCenter ? Mathf.Min(bl.x, tl.x) : Mathf.Max(bl.x, tl.x);
//					float rightX = rightOfCenter ? Mathf.Max(tr.x, br.x) : Mathf.Min(tr.x, br.x);

//					// ── Vertical snapping ───────────────────────────────────
//					bool belowCenter = bl.y < centerLogical.y;
//					bool aboveCenter = tl.y > centerLogical.y;

//					float bottomY = belowCenter ? Mathf.Min(bl.y, br.y) : Mathf.Max(bl.y, br.y);
//					float topY = aboveCenter ? Mathf.Max(tl.y, tr.y) : Mathf.Min(tl.y, tr.y);

//					// ── Optional: force square (largest centered axis-aligned square)
//					float w = rightX - leftX;
//					float h = topY - bottomY;
//					float side = Mathf.Max(w, h);

//					float cx = (leftX + rightX) * 0.5f;
//					float cy = (bottomY + topY) * 0.5f;

//					leftX = cx - side * 0.5f;
//					rightX = cx + side * 0.5f;
//					bottomY = cy - side * 0.5f;
//					topY = cy + side * 0.5f;

//					var depth = 0f;

//					// ── Project to final UV / vertex space ──────────────────
//					vertices[vertIdx + 0] = new Vector3(leftX, bottomY, depth); // BL
//					vertices[vertIdx + 1] = new Vector3(leftX, topY, depth); // TL
//					vertices[vertIdx + 2] = new Vector3(rightX, topY, depth); // TR
//					vertices[vertIdx + 3] = new Vector3(rightX, bottomY, depth); // BR

//					vertIdx += 4;
//				}
//			}

//			//NOW sort the FUCKING QUADS by size

//			// Project the vertices after sort
//			for (var n = 0; n < numRows * numColumns * 4; ++n)
//				vertices[n] = new Vector3(vertices[n].x * uvScaleX, vertices[n].y * uvScaleY, vertices[n].z);

//			// ────────────────────────────────────────────────────────────────
//			//  Finalise mesh
//			// ────────────────────────────────────────────────────────────────

//			_gridMesh.vertices = vertices;
//			_gridMesh.uv = uvs;
//			_gridMesh.colors = colors;
//			_gridMesh.triangles = indices;

//			_lastColumns = numColumns;
//			_lastRows = numRows;
//			_lastCoord = center;
//		}

//		private static void DrawGridToRT(int numColumns, int numRows, Vector2 coord)
//		{
//			LazyInitResources();

//			int pw = numColumns * 64;
//			int ph = numRows * 64;

//			if (_rt == null || _rt.width != pw || _rt.height != ph || !_rt.IsCreated())
//			{
//				if (_rt != null) _rt.Release();
//				_rt = new RenderTexture(pw, ph, 0, RenderTextureFormat.ARGB32)
//				{
//					filterMode = FilterMode.Point,
//					antiAliasing = 1
//				};
//				_rt.Create();
//			}

//			if (_xorTexture == null || _quadMaterial == null) return;

//			RebuildGridMeshIfNeeded(numColumns, numRows, coord);

//			var oldRT = RenderTexture.active;
//			RenderTexture.active = _rt;

//			GL.Clear(true, true, Color.clear);
//			GL.PushMatrix();
//			GL.LoadOrtho();

//			_quadMaterial.mainTexture = _xorTexture;
//			_quadMaterial.color = Color.white;          // base white so vertex colors can darken
//			_quadMaterial.SetPass(0);

//			Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);

//			GL.PopMatrix();
//			RenderTexture.active = oldRT;
//		}

//		public static RenderTexture GetRenderTexture(int numColumns = 8, int numRows = 8, Vector2 coord = default)
//		{
//			if (coord == default) coord = new Vector2(0.5f, 0.5f);
//			DrawGridToRT(numColumns, numRows, coord);
//			return _rt;
//		}

//		public static void Cleanup()
//		{
//			if (_rt != null) { _rt.Release(); _rt = null; }
//			if (_gridMesh != null) { Object.DestroyImmediate(_gridMesh); _gridMesh = null; }
//			if (_quadMaterial != null) { Object.DestroyImmediate(_quadMaterial); _quadMaterial = null; }
//			_xorTexture = null;
//			_lastColumns = _lastRows = -1;
//			_lastCoord = new Vector2(-999f, -999f);
//		}
//	}
//}