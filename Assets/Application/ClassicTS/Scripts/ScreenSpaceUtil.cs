using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ScreenSpaceUtil
	{
		private static RenderTexture _rt;

		private static Mesh _gridMesh;
		private static Material _quadMaterial;
		private static Texture2D _quadTexture;

		private static Mesh _selectedQuadMesh;     // only the highlighted quad
		private static Mesh _outlineMesh;
		private static Material _outlineMaterial;
		private static Texture2D _outlineTexture;

		private static int _lastColumns = -1;
		private static int _lastRows = -1;
		private static Vector2 _lastCoord = new Vector2(-999f, -999f);
		private static Vector2 _lastOutlineCoord = new Vector2(-999f, -999f);

		public static void SetTexture(Texture2D value) => _quadTexture = value;
		public static Texture2D GetTexture() => _quadTexture;

		public static void SetOutlineTexture(Texture2D outlineTex)
		{
			_outlineTexture = outlineTex;
			if (_outlineTexture != null)
				_outlineTexture.filterMode = FilterMode.Bilinear;
		}

		public static void SetOutlineMaterial(Material value)
		{
			_outlineMaterial = value;
			if (value != null && value.mainTexture is Texture2D tex)
				_outlineTexture = tex;
		}

		private static void LazyInitResources()
		{
			if (_quadMaterial == null)
			{
				var shader = TryFindGoodShader();
				if (shader != null)
				{
					_quadMaterial = new Material(shader)
					{
						name = "ScreenSpaceUtil-QuadMat (auto)",
						hideFlags = HideFlags.HideAndDontSave
					};
				}
				else
				{
					Debug.LogError("[ScreenSpaceUtil] No usable shader found for quad material!");
				}
			}

			if (_outlineMaterial == null)
			{
				var shader = TryFindGoodShader();
				if (shader != null)
				{
					_outlineMaterial = new Material(shader)
					{
						name = "ScreenSpaceUtil-OutlineMat (auto)",
						hideFlags = HideFlags.HideAndDontSave
					};

					if (_outlineTexture != null)
						_outlineMaterial.mainTexture = _outlineTexture;
				}
				else
				{
					Debug.LogError("[ScreenSpaceUtil] No usable shader found for outline material!");
				}
			}

			if (_quadTexture == null)
			{
				_quadTexture = TextureUtils.GeneratePerlinNoiseTexture();
				if (_quadTexture != null)
				{
					_quadTexture.filterMode = FilterMode.Point;
					_quadTexture.hideFlags = HideFlags.HideAndDontSave;
				}
			}

			if (_outlineTexture == null)
			{
				_outlineTexture = TextureUtils.GenerateXorTexture256();
				if (_outlineTexture != null)
				{
					_outlineTexture.filterMode = FilterMode.Point;
					_outlineTexture.hideFlags = HideFlags.HideAndDontSave;
				}
			}
		}

		private static Shader TryFindGoodShader()
		{
			return Shader.Find("Sprites/Default")
				?? Shader.Find("Hidden/Internal-Colored")
				?? Shader.Find("Particles/Standard Unlit")
				?? Shader.Find("Universal Render Pipeline/Unlit");
		}

		private static void RebuildGridMeshIfNeeded(int numColumns, int numRows, Vector2 point)
		{
			if (numColumns < 1) numColumns = 1;
			if (numRows < 1) numRows = 1;

			bool needsRebuild = _gridMesh == null ||
								_lastColumns != numColumns ||
								_lastRows != numRows ||
								Vector2.Distance(_lastCoord, point) > 0.0005f;

			if (!needsRebuild) return;

			if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);

			_gridMesh = new Mesh { name = $"DeformGrid_{numColumns}x{numRows}" };

			int totalCells = numColumns * numRows;
			int totalVerts = totalCells * 4;
			int totalTris = totalCells * 6;

			var vertices = new Vector3[totalVerts];
			var uvs = new Vector2[totalVerts];
			var colors = new Color[totalVerts];

			float cellW = 1f;
			float cellH = 1f;
			float uvScaleX = 1f / numColumns;
			float uvScaleY = 1f / numRows;

			Vector2 centerLogical = new Vector2(point.x / uvScaleX, point.y / uvScaleY);

			float falloffRadius = 0.425f / uvScaleY;
			float sqrRadius = falloffRadius * falloffRadius;
			float maxDisplacement = 2f;

			// ───────────────────────────────────────────────────────────────
			// Build deformed + snapped quads (logical space → snapped space)
			// ───────────────────────────────────────────────────────────────
			for (int qy = 0; qy < numRows; qy++)
			{
				for (int qx = 0; qx < numColumns; qx++)
				{
					int qIdx = qy * numColumns + qx;
					int baseVert = qIdx * 4;

					float x0 = qx * cellW;
					float y0 = qy * cellH;
					float x1 = x0 + cellW;
					float y1 = y0 + cellH;

					Vector2 quadCenter = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);

					float dt = 0.25f * (
						(new Vector2(x0, y0) - centerLogical).sqrMagnitude +
						(new Vector2(x0, y1) - centerLogical).sqrMagnitude +
						(new Vector2(x1, y1) - centerLogical).sqrMagnitude +
						(new Vector2(x1, y0) - centerLogical).sqrMagnitude);

					float scaleStrength = dt < sqrRadius
						? (1f - dt / sqrRadius) * (1f - dt / sqrRadius) * maxDisplacement
						: 0f;

					float deltaScale = 1f + scaleStrength;
					float transScale = scaleStrength * 0.375f;

					Vector3 bl = quadCenter + (new Vector2(x0, y0) - quadCenter) * deltaScale + (new Vector2(x0, y0) - centerLogical) * transScale;
					Vector3 tl = quadCenter + (new Vector2(x0, y1) - quadCenter) * deltaScale + (new Vector2(x0, y1) - centerLogical) * transScale;
					Vector3 tr = quadCenter + (new Vector2(x1, y1) - quadCenter) * deltaScale + (new Vector2(x1, y1) - centerLogical) * transScale;
					Vector3 br = quadCenter + (new Vector2(x1, y0) - quadCenter) * deltaScale + (new Vector2(x1, y0) - centerLogical) * transScale;

					// Snap to largest axis-aligned square
					float minX = Mathf.Min(bl.x, tl.x);
					float maxX = Mathf.Max(tr.x, br.x);
					float minY = Mathf.Min(bl.y, br.y);
					float maxY = Mathf.Max(tl.y, tr.y);

					float side = Mathf.Max(maxX - minX, maxY - minY);
					float cx = (minX + maxX) * 0.5f;
					float cy = (minY + maxY) * 0.5f;

					vertices[baseVert + 0] = new Vector3(cx - side * 0.5f, cy - side * 0.5f, 0f);
					vertices[baseVert + 1] = new Vector3(cx - side * 0.5f, cy + side * 0.5f, 0f);
					vertices[baseVert + 2] = new Vector3(cx + side * 0.5f, cy + side * 0.5f, 0f);
					vertices[baseVert + 3] = new Vector3(cx + side * 0.5f, cy - side * 0.5f, 0f);

					uvs[baseVert + 0] = new Vector2(x0 * uvScaleX, y0 * uvScaleY);
					uvs[baseVert + 1] = new Vector2(x0 * uvScaleX, y1 * uvScaleY);
					uvs[baseVert + 2] = new Vector2(x1 * uvScaleX, y1 * uvScaleY);
					uvs[baseVert + 3] = new Vector2(x1 * uvScaleX, y0 * uvScaleY);

					colors[baseVert + 0] = colors[baseVert + 1] =
					colors[baseVert + 2] = colors[baseVert + 3] = Color.white;
				}
			}

			// ───────────────────────────────────────────────────────────────
			// Sort quads by area (ascending) — painter's algorithm prep
			// Largest (most deformed) ends up last
			// ───────────────────────────────────────────────────────────────
			var quads = new List<(float area, int srcBase, int origIdx)>(totalCells);

			for (int q = 0; q < totalCells; q++)
			{
				int b = q * 4;
				float w = vertices[b + 2].x - vertices[b + 0].x;
				float h = vertices[b + 2].y - vertices[b + 0].y;
				quads.Add((w * h, b, q));
			}

			quads.Sort((a, b) => a.area.CompareTo(b.area));

			var sortedVerts = new Vector3[totalVerts];
			var sortedUVs = new Vector2[totalVerts];
			var sortedColors = new Color[totalVerts];
			var sortedIndices = new int[totalTris];

			for (int i = 0; i < quads.Count; i++)
			{
				var (_, oldBase, _) = quads[i];
				int newBase = i * 4;

				for (int c = 0; c < 4; c++)
				{
					sortedVerts[newBase + c] = vertices[oldBase + c];
					sortedUVs[newBase + c] = uvs[oldBase + c];
					sortedColors[newBase + c] = colors[oldBase + c];
				}

				int tri = i * 6;
				sortedIndices[tri + 0] = newBase;
				sortedIndices[tri + 1] = newBase + 1;
				sortedIndices[tri + 2] = newBase + 2;
				sortedIndices[tri + 3] = newBase;
				sortedIndices[tri + 4] = newBase + 2;
				sortedIndices[tri + 5] = newBase + 3;
			}

			// Final scale to normalized [0..1] space
			for (int i = 0; i < totalVerts; i++)
			{
				var v = sortedVerts[i];
				sortedVerts[i] = new Vector3(v.x * uvScaleX, v.y * uvScaleY, v.z);
			}

			_gridMesh.vertices = sortedVerts;
			_gridMesh.uv = sortedUVs;
			_gridMesh.colors = sortedColors;
			_gridMesh.triangles = sortedIndices;

			_lastColumns = numColumns;
			_lastRows = numRows;
			_lastCoord = point;

			// ───────────────────────────────────────────────────────────────
			// Highlight & outline = largest quad (last in sorted list)
			// ───────────────────────────────────────────────────────────────
			bool highlightNeedsRebuild = _selectedQuadMesh == null || _outlineMesh == null ||
										 Vector2.Distance(_lastOutlineCoord, point) > 0.0005f ||
										 _lastColumns != numColumns ||
										 _lastRows != numRows;

			if (!highlightNeedsRebuild) return;

			if (_selectedQuadMesh != null) Object.DestroyImmediate(_selectedQuadMesh);
			if (_outlineMesh != null) Object.DestroyImmediate(_outlineMesh);

			_selectedQuadMesh = null;
			_outlineMesh = null;

			// Skip if center is outside grid bounds
			Vector2 coordLogical = new Vector2(point.x * numColumns, point.y * numRows);
			int highlightQx = Mathf.FloorToInt(coordLogical.x);
			int highlightQy = Mathf.FloorToInt(coordLogical.y);

			if (highlightQx < 0 || highlightQx >= numColumns ||
				highlightQy < 0 || highlightQy >= numRows)
				return;

			// The largest quad is always the last one after area sort
			int lastQuadIndex = quads.Count - 1;
			int baseVertLast = lastQuadIndex * 4;

			Vector3 v0 = sortedVerts[baseVertLast + 0];
			Vector3 v1 = sortedVerts[baseVertLast + 1];
			Vector3 v2 = sortedVerts[baseVertLast + 2];
			Vector3 v3 = sortedVerts[baseVertLast + 3];

			_selectedQuadMesh = new Mesh
			{
				vertices = new[] { v0, v1, v2, v3 },
				uv = new[] { sortedUVs[baseVertLast + 0], sortedUVs[baseVertLast + 1], sortedUVs[baseVertLast + 2], sortedUVs[baseVertLast + 3] },
				triangles = new[] { 0, 1, 2, 0, 2, 3 }
			};

			// Outline — outset from the same deformed quad
			const float outlineScale = 1.54f;//magic number based on highlight texture / material

			Vector3 center = (v0 + v1 + v2 + v3) * 0.25f;

			Vector3 ov0 = center + (v0 - center) * outlineScale;
			Vector3 ov1 = center + (v1 - center) * outlineScale;
			Vector3 ov2 = center + (v2 - center) * outlineScale;
			Vector3 ov3 = center + (v3 - center) * outlineScale;

			_outlineMesh = new Mesh
			{
				vertices = new[] { ov0, ov1, ov2, ov3 },
				uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
				triangles = new[] { 0, 1, 2, 0, 2, 3 }
			};

			_lastOutlineCoord = point;
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

			RebuildGridMeshIfNeeded(numColumns, numRows, coord);

			var oldRT = RenderTexture.active;
			RenderTexture.active = _rt;

			GL.Clear(true, true, new Color(0.12f, 0.14f, 0.16f));

			GL.PushMatrix();
			GL.LoadOrtho();

			if (_gridMesh != null)
			{
				_quadMaterial.mainTexture = _quadTexture;
				_quadMaterial.color = Color.white;
				_quadMaterial.SetPass(0);
				Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);
			}

			if (_selectedQuadMesh != null)
			{
				_quadMaterial.mainTexture = null;
				_quadMaterial.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
				_quadMaterial.SetPass(0);
				Graphics.DrawMeshNow(_selectedQuadMesh, Matrix4x4.identity);
			}

			if (_selectedQuadMesh != null)
			{
				_quadMaterial.mainTexture = _quadTexture;
				_quadMaterial.color = Color.white;
				_quadMaterial.SetPass(0);
				Graphics.DrawMeshNow(_selectedQuadMesh, Matrix4x4.identity);
			}

			if (_outlineMesh != null && _outlineMaterial != null)
			{
				_outlineMaterial.SetPass(0);
				Graphics.DrawMeshNow(_outlineMesh, Matrix4x4.identity);
			}

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
			if (_rt != null)
			{
				_rt.Release();
				_rt = null;
			}

			if (_gridMesh != null) { Object.DestroyImmediate(_gridMesh); _gridMesh = null; }
			if (_selectedQuadMesh != null) { Object.DestroyImmediate(_selectedQuadMesh); _selectedQuadMesh = null; }
			if (_outlineMesh != null) { Object.DestroyImmediate(_outlineMesh); _outlineMesh = null; }

			_quadMaterial = null;
			_outlineMaterial = null;
			_quadTexture = null;
			_outlineTexture = null;

			_lastColumns = _lastRows = -1;
			_lastCoord = new Vector2(-999f, -999f);
			_lastOutlineCoord = new Vector2(-999f, -999f);
		}
	}
}