using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ScreenSpaceUtil
	{
		private static RenderTexture _mainRT;                   // full grid including enlarged quad — now with extra border
		private static RenderTexture _selectedRT;               // only the enlarged quad, size CELL_SIZE * SELECTED_SIZE

		private static Mesh _gridMesh;
		private static Material _quadMaterial;
		private static Texture2D _quadTexture;

		private static Mesh _selectedQuadMesh;                  // reused for both main & selected RT
		private static Vector2 _selectedQuadMeshPos;
		private static Vector3[] _selectedQuadMeshverts;

		private static Mesh _outlineMesh;
		private static Material _outlineMaterial;
		private static Texture2D _outlineTexture;

		private static int _lastColumns = -1;
		private static int _lastRows = -1;
		private static Vector2 _lastCoord = new Vector2(-999f, -999f);

		public static void SetTexture(Texture2D value) => _quadTexture = value;
		public static Texture2D GetTexture() => _quadTexture;

		private const int CELL_SIZE = 128;
		private const float SELECTED_SIZE = 4f;
		private const float DELTA_TRANS_RATIO = 0.375f;

		private const int BORDER = 256;//128;  // extra pixels around the grid for overdraw (glow/outline/shadow/...)

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
			if (_selectedQuadMesh != null) Object.DestroyImmediate(_selectedQuadMesh);
			if (_outlineMesh != null) Object.DestroyImmediate(_outlineMesh);
			//if (_backgroundMesh != null) Object.DestroyImmediate(_backgroundMesh);

			_gridMesh = new Mesh { name = $"DeformGrid_{numColumns}x{numRows}" };
			_selectedQuadMesh = null;
			_outlineMesh = null;
			//_backgroundMesh = null;

			int corePixelWidth = numColumns * CELL_SIZE;
			int corePixelHeight = numRows * CELL_SIZE;
			int paddedPixelWidth = corePixelWidth + 2 * BORDER;
			int paddedPixelHeight = corePixelHeight + 2 * BORDER;

			// How much of the padded RT is actual content (normalized)
			float contentScaleX = (float)corePixelWidth / paddedPixelWidth;
			float contentScaleY = (float)corePixelHeight / paddedPixelHeight;

			// Offset to center the content inside the padded texture
			float contentOffsetX = (float)BORDER / paddedPixelWidth;
			float contentOffsetY = (float)BORDER / paddedPixelHeight;

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
			float falloffRadius = 0.625f / uvScaleY;
			float sqrRadius = falloffRadius * falloffRadius;
			float maxDisplacement = 1f;

			var quadData = new List<(float scaleStrength, int qIdx)>(totalCells);

			for (var qy = 0; qy < numRows; qy++)
			{
				for (var qx = 0; qx < numColumns; qx++)
				{
					var x = (qx + 0.5f) * cellW;
					var y = (qy + 0.5f) * cellH;
					var d = (new Vector2(x, y) - centerLogical).sqrMagnitude;
					var scale = d < sqrRadius ? (1f - d / sqrRadius) * (1f - d / sqrRadius) * maxDisplacement : 0f;

					quadData.Add((scale, qy * numColumns + qx));
				}
			}

			quadData.Sort((a, b) => a.scaleStrength.CompareTo(b.scaleStrength));

			for (int drawIdx = 0; drawIdx < quadData.Count; drawIdx++)
			{
				var (scaleStrength, qIdx) = quadData[drawIdx];

				int qx = qIdx % numColumns;
				int qy = qIdx / numColumns;

				float x0 = qx * cellW;
				float y0 = qy * cellH;
				float x1 = x0 + cellW;
				float y1 = y0 + cellH;

				Vector2 quadCenter = new((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);

				float deltaScale = 1f + scaleStrength;
				float transScale = scaleStrength * DELTA_TRANS_RATIO;

				if (!invalid && drawIdx == quadData.Count - 1)
					deltaScale = SELECTED_SIZE;

				int baseVert = drawIdx * 4;

				vertices[baseVert + 0] = quadCenter + (new Vector2(x0, y0) - quadCenter) * deltaScale + (new Vector2(x0, y0) - centerLogical) * transScale;
				vertices[baseVert + 1] = quadCenter + (new Vector2(x0, y1) - quadCenter) * deltaScale + (new Vector2(x0, y1) - centerLogical) * transScale;
				vertices[baseVert + 2] = quadCenter + (new Vector2(x1, y1) - quadCenter) * deltaScale + (new Vector2(x1, y1) - centerLogical) * transScale;
				vertices[baseVert + 3] = quadCenter + (new Vector2(x1, y0) - quadCenter) * deltaScale + (new Vector2(x1, y0) - centerLogical) * transScale;

				//apply texture_translate and texture_scale here

				uvs[baseVert + 0] = new Vector2(x0 * uvScaleX, y0 * uvScaleY);
				uvs[baseVert + 1] = new Vector2(x0 * uvScaleX, y1 * uvScaleY);
				uvs[baseVert + 2] = new Vector2(x1 * uvScaleX, y1 * uvScaleY);
				uvs[baseVert + 3] = new Vector2(x1 * uvScaleX, y0 * uvScaleY);

				var luminense = 1f - scaleStrength * 0.75f;
				var colour = new Color(luminense, luminense, luminense, 1);

				colors[baseVert + 0] = colors[baseVert + 1] =
				colors[baseVert + 2] = colors[baseVert + 3] = colour;// Color.white;

				int tri = drawIdx * 6;
				indices[tri + 0] = baseVert + 0;
				indices[tri + 1] = baseVert + 1;
				indices[tri + 2] = baseVert + 2;
				indices[tri + 3] = baseVert + 0;
				indices[tri + 4] = baseVert + 2;
				indices[tri + 5] = baseVert + 3;
			}

			for (int i = 0; i < totalVerts; i++)
			{
				var v = vertices[i];

				// First apply original logical-to-UV scaling (as it was before)
				float scaledX = v.x * uvScaleX;
				float scaledY = v.y * uvScaleY;

				// Then apply padding: shrink towards center + add border offset
				float finalX = contentOffsetX + scaledX * contentScaleX;
				float finalY = contentOffsetY + scaledY * contentScaleY;

				vertices[i] = new Vector3(finalX, finalY, v.z);
			}

			_gridMesh.vertices = vertices;
			_gridMesh.uv = uvs;
			_gridMesh.colors = colors;
			_gridMesh.triangles = indices;

			_lastColumns = numColumns;
			_lastRows = numRows;
			_lastCoord = point;

			// ───────────────────────────────────────────────────────────────
			// Create separate mesh + RT only for the enlarged quad
			// ───────────────────────────────────────────────────────────────
			if (_selectedQuadMesh != null) Object.DestroyImmediate(_selectedQuadMesh);
			//if (_backgroundMesh != null) Object.DestroyImmediate(_backgroundMesh);

			_selectedQuadMesh = null;
			//_backgroundMesh = null;

			//// Background behind selection
			//var backgroundScaleX = 7f * uvScaleX;
			//var backgroundScaleY = 7f * uvScaleY;

			//var bv0 = new Vector3(point.x - backgroundScaleX, point.y - backgroundScaleY, 0);
			//var bv1 = new Vector3(point.x - backgroundScaleX, point.y + backgroundScaleY, 0);
			//var bv2 = new Vector3(point.x + backgroundScaleX, point.y + backgroundScaleY, 0);
			//var bv3 = new Vector3(point.x + backgroundScaleX, point.y - backgroundScaleY, 0);

			//_backgroundMesh = new Mesh
			//{
			//	vertices = new[] { bv0, bv1, bv2, bv3 },
			//	uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
			//	triangles = new[] { 0, 1, 2, 0, 2, 3 }
			//};

			if (invalid) return;

			var lastBase = (quadData.Count - 1) * 4;

			_selectedQuadMeshPos = (vertices[lastBase + 0] + vertices[lastBase + 1] + vertices[lastBase + 2] + vertices[lastBase + 3]) * 0.25f;
			_selectedQuadMeshverts = new[] { vertices[lastBase + 0], vertices[lastBase + 1], vertices[lastBase + 2], vertices[lastBase + 3] };

			var uv0 = uvs[lastBase + 0];
			var uv1 = uvs[lastBase + 1];
			var uv2 = uvs[lastBase + 2];
			var uv3 = uvs[lastBase + 3];

			var v0 = new Vector3(0, 0, 0);
			var v1 = new Vector3(0, 1, 0);
			var v2 = new Vector3(1, 1, 0);
			var v3 = new Vector3(1, 0, 0);

			_selectedQuadMesh = new Mesh
			{
				vertices = new[] { v0, v1, v2, v3 },
				uv = new[] { uv0, uv1, uv2, uv3 },
				triangles = new[] { 0, 1, 2, 0, 2, 3 }
			};
		}

		private static void DrawGridToRT(int numColumns, int numRows, Vector2 coord)
		{
			LazyInitResources();

			int coreWidth = numColumns * CELL_SIZE;
			int coreHeight = numRows * CELL_SIZE;
			int paddedWidth = coreWidth + 2 * BORDER;
			int paddedHeight = coreHeight + 2 * BORDER;

			if (_mainRT == null || _mainRT.width != paddedWidth || _mainRT.height != paddedHeight || !_mainRT.IsCreated())
			{
				if (_mainRT != null) _mainRT.Release();
				_mainRT = new RenderTexture(paddedWidth, paddedHeight, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Bilinear,
					antiAliasing = 1
				};
				_mainRT.Create();
			}

			RebuildGridMeshIfNeeded(numColumns, numRows, coord);

			var oldRT = RenderTexture.active;
			RenderTexture.active = _mainRT;

			//GL.Clear(true, true, new Color(0.12f, 0.14f, 0.16f));//debug
			GL.Clear(true, true, Color.clear);

			GL.PushMatrix();
			GL.LoadOrtho();

			if (_gridMesh != null)
			{
				_quadMaterial.mainTexture = _quadTexture;
				_quadMaterial.color = Color.white;
				_quadMaterial.SetPass(0);
				Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);
			}

			GL.PopMatrix();
			RenderTexture.active = oldRT;
		}

		private static void DrawSelectedOnlyToRT(int numColumns, int numRows, Vector2 coord)
		{
			if (_selectedQuadMesh == null) return;

			int selSize = Mathf.CeilToInt(CELL_SIZE * SELECTED_SIZE);

			if (_selectedRT == null || _selectedRT.width != selSize || _selectedRT.height != selSize || !_selectedRT.IsCreated())
			{
				if (_selectedRT != null) _selectedRT.Release();
				_selectedRT = new RenderTexture(selSize, selSize, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Bilinear,
					antiAliasing = 1
				};
				_selectedRT.Create();
			}

			var old = RenderTexture.active;
			RenderTexture.active = _selectedRT;

			GL.Clear(true, true, new Color(0, 0, 0, 0));

			GL.PushMatrix();
			GL.LoadOrtho();

			if (_selectedQuadMesh != null)
			{
				_quadMaterial.mainTexture = _quadTexture;
				_quadMaterial.color = Color.white;
				_quadMaterial.SetPass(0);
				Graphics.DrawMeshNow(_selectedQuadMesh, Matrix4x4.identity);
			}

			GL.PopMatrix();
			RenderTexture.active = old;
		}

		public static void OnGUI(Rect rect, int numColumns = 8, int numRows = 8, Vector2 coord = default)
		{
			if (coord == default) coord = new Vector2(0.5f, 0.5f);

			DrawGridToRT(numColumns, numRows, coord);
			DrawSelectedOnlyToRT(numColumns, numRows, coord);

			// Main grid (with deformed cells) — offset to compensate for padding
			if (_mainRT != null)
			{
				var renderW = _mainRT.width - BORDER * 2;
				var borderX = (float)BORDER * _mainRT.width / renderW;
				var renderH = _mainRT.height - BORDER * 2;
				var borderY = (float)BORDER * _mainRT.height / renderH;

				borderX *= rect.width / _mainRT.width;
				borderY *= rect.height / _mainRT.height;

				var displayRect = new Rect(
					rect.x - borderX,
					rect.y - borderY,
					rect.width + borderX * 2,
					rect.height + borderY * 2
				);

				GUI.DrawTexture(displayRect.ToGUIRect(), _mainRT, ScaleMode.ScaleAndCrop, true);
			}

			// Enlarged selected cell on top — centered on the cell position
			if (_selectedRT != null && _selectedQuadMesh != null)
			{
				float quadWidth = (_selectedQuadMeshverts[2].x - _selectedQuadMeshverts[0].x) * _mainRT.width;
				float quadHeight = (_selectedQuadMeshverts[1].y - _selectedQuadMeshverts[0].y) * _mainRT.height;

				quadWidth *= rect.width / (_mainRT.width - BORDER * 2);
				quadHeight *= rect.height / (_mainRT.height - BORDER * 2);

				var renderW = _mainRT.width - BORDER * 2;
				var borderX = (float)BORDER * _mainRT.width / renderW;
				var renderH = _mainRT.height - BORDER * 2;
				var borderY = (float)BORDER * _mainRT.height / renderH;

				borderX *= rect.width / _mainRT.width;
				borderY *= rect.height / _mainRT.height;

				float selectedPosX = (_selectedQuadMeshverts[2].x + _selectedQuadMeshverts[0].x) * 0.5f * _mainRT.width;
				float selectedPosY = (_selectedQuadMeshverts[1].y + _selectedQuadMeshverts[0].y) * 0.5f * _mainRT.height;

				selectedPosX *= rect.width / (_mainRT.width - BORDER * 2);
				selectedPosY *= rect.height / (_mainRT.height - BORDER * 2);

				float centerX = selectedPosX - borderX + rect.x;
				float centerY = selectedPosY - borderY + rect.y;

				var selRect = new Rect(
					centerX - quadWidth * 0.5f,
					centerY - quadHeight * 0.5f,
					quadWidth,
					quadHeight
				);

				GUI.DrawTexture(selRect.ToGUIRect(), _selectedRT, ScaleMode.StretchToFill, true);
			}
		}

		public static RenderTexture GetRenderTexture(int numColumns = 8, int numRows = 8, Vector2 coord = default)
		{
			if (coord == default) coord = new Vector2(0.5f, 0.5f);
			DrawGridToRT(numColumns, numRows, coord);
			return _mainRT;
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

			static Shader TryFindGoodShader()
			{
				return Shader.Find("Sprites/Default")
					?? Shader.Find("Hidden/Internal-Colored")
					?? Shader.Find("Particles/Standard Unlit")
					?? Shader.Find("Universal Render Pipeline/Unlit");
			}
		}

		public static void Cleanup()
		{
			if (_mainRT != null)
			{
				_mainRT.Release();
				_mainRT = null;
			}
			if (_selectedRT != null)
			{
				_selectedRT.Release();
				_selectedRT = null;
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
		}
	}
}