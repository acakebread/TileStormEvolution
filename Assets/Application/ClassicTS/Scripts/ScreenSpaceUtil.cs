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

		private static Mesh _backgroundMesh;
		private static Material _backgroundMaterial;
		private static Texture2D _backgroundTexture;

		private static int _lastColumns = -1;
		private static int _lastRows = -1;
		private static Vector2 _lastCoord = new Vector2(-999f, -999f);

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

		public static void SetBackgroundMaterial(Material value)
		{
			_backgroundMaterial = value;
			if (value != null && value.mainTexture is Texture2D tex)
				_backgroundTexture = tex;
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

			if (_backgroundMaterial == null)
			{
				var shader = TryFindGoodShader();
				if (shader != null)
				{
					_backgroundMaterial = new Material(shader)
					{
						name = "ScreenSpaceUtil-OutlineMat (auto)",
						hideFlags = HideFlags.HideAndDontSave
					};

					if (_backgroundMaterial != null)
						_backgroundMaterial.mainTexture = _backgroundTexture;
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

			if (_backgroundTexture == null)
			{
				_backgroundTexture = TextureUtils.GenerateXorTexture256();
				if (_backgroundTexture != null)
				{
					_backgroundTexture.filterMode = FilterMode.Point;
					_backgroundTexture.hideFlags = HideFlags.HideAndDontSave;
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
			var indices = new int[totalTris];

			var invalid = point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1;

			Vector2 centerLogical = new Vector2(point.x * numColumns, point.y * numRows);

			float cellW = 1f;
			float cellH = 1f;
			float uvScaleX = 1f / numColumns;
			float uvScaleY = 1f / numRows;
			//float falloffRadius = 0.425f / uvScaleY;
			float falloffRadius = 0.625f / uvScaleY;
			float sqrRadius = falloffRadius * falloffRadius;
			float maxDisplacement = 1f;

			// Phase 1: collect scale strengths
			var quadData = new List<(float scaleStrength, int qIdx)>(totalCells);

			for (int qy = 0; qy < numRows; qy++)
			{
				for (int qx = 0; qx < numColumns; qx++)
				{
					int qIdx = qy * numColumns + qx;

					float x0 = qx * cellW;
					float y0 = qy * cellH;
					float x1 = x0 + cellW;
					float y1 = y0 + cellH;

					float dt = 0.25f * (
						(new Vector2(x0, y0) - centerLogical).sqrMagnitude +
						(new Vector2(x0, y1) - centerLogical).sqrMagnitude +
						(new Vector2(x1, y1) - centerLogical).sqrMagnitude +
						(new Vector2(x1, y0) - centerLogical).sqrMagnitude);

					float scaleStrength = dt < sqrRadius
						? (1f - dt / sqrRadius) * (1f - dt / sqrRadius) * maxDisplacement
						: 0f;

					quadData.Add((scaleStrength, qIdx));
				}
			}

			// Sort ascending by strength → small → large (painter's: large on top)
			quadData.Sort((a, b) => a.scaleStrength.CompareTo(b.scaleStrength));

			// Phase 2: build final buffers directly
			for (int drawIdx = 0; drawIdx < quadData.Count; drawIdx++)
			{
				var (scaleStrength, qIdx) = quadData[drawIdx];

				int qx = qIdx % numColumns;
				int qy = qIdx / numColumns;

				float x0 = qx * cellW;
				float y0 = qy * cellH;
				float x1 = x0 + cellW;
				float y1 = y0 + cellH;

				Vector2 quadCenter = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);

				float deltaScale = 1f + scaleStrength;
				float transScale = scaleStrength * 0.375f;

				if (!invalid && drawIdx == quadData.Count - 1)
				{
					deltaScale = 5f;// maxDisplacement + 1f;
					//transScale *= 0.25f;
				}

				int baseVert = drawIdx * 4;

				vertices[baseVert + 0] = quadCenter + (new Vector2(x0, y0) - quadCenter) * deltaScale + (new Vector2(x0, y0) - centerLogical) * transScale;
				vertices[baseVert + 1] = quadCenter + (new Vector2(x0, y1) - quadCenter) * deltaScale + (new Vector2(x0, y1) - centerLogical) * transScale;
				vertices[baseVert + 2] = quadCenter + (new Vector2(x1, y1) - quadCenter) * deltaScale + (new Vector2(x1, y1) - centerLogical) * transScale;
				vertices[baseVert + 3] = quadCenter + (new Vector2(x1, y0) - quadCenter) * deltaScale + (new Vector2(x1, y0) - centerLogical) * transScale;

				uvs[baseVert + 0] = new Vector2(x0 * uvScaleX, y0 * uvScaleY);
				uvs[baseVert + 1] = new Vector2(x0 * uvScaleX, y1 * uvScaleY);
				uvs[baseVert + 2] = new Vector2(x1 * uvScaleX, y1 * uvScaleY);
				uvs[baseVert + 3] = new Vector2(x1 * uvScaleX, y0 * uvScaleY);

				colors[baseVert + 0] = colors[baseVert + 1] =
				colors[baseVert + 2] = colors[baseVert + 3] = Color.white;

				int tri = drawIdx * 6;
				indices[tri + 0] = baseVert + 0;
				indices[tri + 1] = baseVert + 1;
				indices[tri + 2] = baseVert + 2;
				indices[tri + 3] = baseVert + 0;
				indices[tri + 4] = baseVert + 2;
				indices[tri + 5] = baseVert + 3;
			}

			// Final scale
			for (int i = 0; i < totalVerts; i++)
			{
				var v = vertices[i];
				vertices[i] = new Vector3(v.x * uvScaleX, v.y * uvScaleY, v.z);
			}

			_gridMesh.vertices = vertices;
			_gridMesh.uv = uvs;
			_gridMesh.colors = colors;
			_gridMesh.triangles = indices;

			_lastColumns = numColumns;
			_lastRows = numRows;
			_lastCoord = point;

			// ───────────────────────────────────────────────────────────────
			// Highlight & outline = the largest quad (last in sorted list)
			// ───────────────────────────────────────────────────────────────
			if (_selectedQuadMesh != null) Object.DestroyImmediate(_selectedQuadMesh);
			if (_outlineMesh != null) Object.DestroyImmediate(_outlineMesh);
			if (_backgroundMesh != null) Object.DestroyImmediate(_backgroundMesh);

			_selectedQuadMesh = null;
			_outlineMesh = null;
			_backgroundMesh = null;

			var backgroundScaleX = 7f * uvScaleX;
			var backgroundScaleY = 7f * uvScaleY;

			var bv0 = new Vector3(point.x - backgroundScaleX, point.y - backgroundScaleY, 0);
			var bv1 = new Vector3(point.x - backgroundScaleX, point.y + backgroundScaleY, 0);
			var bv2 = new Vector3(point.x + backgroundScaleX, point.y + backgroundScaleY, 0);
			var bv3 = new Vector3(point.x + backgroundScaleX, point.y - backgroundScaleY, 0);

			_backgroundMesh = new Mesh
			{
				vertices = new[] { bv0, bv1, bv2, bv3 },
				uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
				triangles = new[] { 0, 1, 2, 0, 2, 3 }
			};

			if (invalid)
				return;

			// Largest quad = last position
			int lastBase = (quadData.Count - 1) * 4;

			Vector3 v0 = vertices[lastBase + 0];
			Vector3 v1 = vertices[lastBase + 1];
			Vector3 v2 = vertices[lastBase + 2];
			Vector3 v3 = vertices[lastBase + 3];

			// UVs: since it's the largest, we don't need exact logical qx/qy for UVs if the texture is uniform
			// But if you need correct region sampling, use the original UVs from the logical quad (but you said it's the largest, so assume uniform or skip UV remap)
			// For now, use full texture UVs for highlight as per your original outline intent

			_selectedQuadMesh = new Mesh
			{
				vertices = new[] { v0, v1, v2, v3 },
				uv = new[] { uvs[lastBase + 0], uvs[lastBase + 1], uvs[lastBase + 2], uvs[lastBase + 3] },
				triangles = new[] { 0, 1, 2, 0, 2, 3 }
			};

			// Outline — simple uniform scale
			const float outlineScale = 1.56f;
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

			//if (_selectedQuadMesh != null)
			//{
			//	_quadMaterial.mainTexture = null;
			//	_quadMaterial.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
			//	_quadMaterial.SetPass(0);
			//	Graphics.DrawMeshNow(_selectedQuadMesh, Matrix4x4.identity);
			//}

			if (_backgroundMesh != null && _backgroundMaterial != null)
			{
				_backgroundMaterial.SetPass(0);
				Graphics.DrawMeshNow(_backgroundMesh, Matrix4x4.identity);
			}

			if (_selectedQuadMesh != null)
			{
				_quadMaterial.mainTexture = _quadTexture;
				_quadMaterial.color = Color.white;
				_quadMaterial.SetPass(0);
				Graphics.DrawMeshNow(_selectedQuadMesh, Matrix4x4.identity);
			}

			//if (_outlineMesh != null && _outlineMaterial != null)
			//{
			//	_outlineMaterial.SetPass(0);
			//	Graphics.DrawMeshNow(_outlineMesh, Matrix4x4.identity);
			//}

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
			_backgroundMaterial = null;
			_quadTexture = null;
			_outlineTexture = null;
			_backgroundTexture = null;

			_lastColumns = _lastRows = -1;
			_lastCoord = new Vector2(-999f, -999f);
		}
	}
}