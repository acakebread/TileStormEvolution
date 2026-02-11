using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ScreenSpaceUtil
	{
		private static RenderTexture _mainRT;
		private static Mesh _gridMesh;
		private static RenderTexture _selectedRT;
		private static Mesh _selectedMesh;
		private static Material _renderMaterial;

		private static Vector2 _lastCoord = new Vector2(-999f, -999f);

		public const int MARGIN = 256;
		private const float SELECTED_SIZE = 4f;
		private const float DELTA_TRANS_RATIO = 0.375f;

		private static int _selectedQuadVertexBase = -1; // index in _gridMesh.vertices, -1 = none

		public static void OnGUI(IGridAtlas atlas, Rect rect, Vector2 coord = default)
		{
			if (atlas == null || atlas.Texture == null) return;

			if (coord == default) coord = new Vector2(0.5f, 0.5f);

			LazyInitResources();

			// Common values — computed once
			int columns = Mathf.Max(1, atlas.Columns);
			int rows = Mathf.Max(1, atlas.Rows);
			int cellSize = atlas.CellSize;

			int coreW = columns * cellSize;
			int coreH = rows * cellSize;
			int padW = coreW + 2 * MARGIN;
			int padH = coreH + 2 * MARGIN;

			float scaleW = (float)rect.width / coreW;
			float scaleH = (float)rect.height / coreH;

			float borderX = MARGIN * scaleW;
			float borderY = MARGIN * scaleH;
			float contentScaleX = (float)coreW / padW;
			float contentScaleY = (float)coreH / padH;
			float contentOffsetX = (float)MARGIN / padW;
			float contentOffsetY = (float)MARGIN / padH;

			if (_mainRT == null || _mainRT.width != padW || _mainRT.height != padH)
			{
				_mainRT?.Release();
				_mainRT = new RenderTexture(padW, padH, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Bilinear
				};
				_mainRT.Create();
			}

			// ─── Draw ────────────────────────────────────────────────────────────────
			DrawGridToRT();

			if (_mainRT == null) return;

			var displayRect = new Rect(
				rect.x - borderX,
				rect.y - borderY,
				rect.width + borderX * 2,
				rect.height + borderY * 2
			);

			GUI.DrawTexture(displayRect.ToGUIRect(), _mainRT, ScaleMode.ScaleToFit, true);

			// Selected overlay — now using grid mesh vertices directly
			if (_selectedRT == null || _selectedQuadVertexBase < 0 || _gridMesh == null) return;

			var verts = _gridMesh.vertices;

			Vector3 bl = verts[_selectedQuadVertexBase + 0];
			Vector3 tl = verts[_selectedQuadVertexBase + 1];
			Vector3 tr = verts[_selectedQuadVertexBase + 2];

			float quadWidth = (tr.x - bl.x) * _mainRT.width * scaleW;
			float quadHeight = (tl.y - bl.y) * _mainRT.height * scaleH;

			float selectedPosX = (bl.x + tr.x) * 0.5f * _mainRT.width * scaleW;
			float selectedPosY = (bl.y + tl.y) * 0.5f * _mainRT.height * scaleH;

			float centerX = selectedPosX - borderX + rect.x;
			float centerY = selectedPosY - borderY + rect.y;

			var selRect = new Rect(centerX - quadWidth * 0.5f, centerY - quadHeight * 0.5f, quadWidth, quadHeight);
			GUI.DrawTexture(selRect.ToGUIRect(), _selectedRT, ScaleMode.ScaleToFit, true);

			// ─── Local helpers ───────────────────────────────────────────────────────

			void DrawGridToRT()
			{
				bool invalid = coord.x < 0 || coord.x > 1 || coord.y < 0 || coord.y > 1;
				bool coordChanged = (_lastCoord - coord).sqrMagnitude > 0.00000025f;

				if (_gridMesh == null || coordChanged)
				{
					_lastCoord = coord;

					if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);
					_gridMesh = new Mesh { name = $"DeformGrid_{columns}x{rows}" };

					if (invalid)
					{
						_selectedQuadVertexBase = -1;
						if (_selectedMesh != null)
						{
							Object.DestroyImmediate(_selectedMesh);
							_selectedMesh = null;
						}
					}

					int totalCells = columns * rows;

					var vertices = new Vector3[totalCells * 4];
					var uvs = new Vector2[totalCells * 4];
					var colors = new Color[totalCells * 4];
					var indices = new int[totalCells * 6];

					Vector2 centerLogical = new(coord.x * columns, coord.y * rows);

					float uvScaleX = 1f / columns;
					float uvScaleY = 1f / rows;
					float falloffRadius = 0.625f / uvScaleY;
					float sqrRadius = falloffRadius * falloffRadius;

					var quadData = new List<(float strength, int qIdx)>(totalCells);

					for (int qy = 0; qy < rows; qy++)
						for (int qx = 0; qx < columns; qx++)
						{
							float cx = qx + 0.5f;
							float cy = qy + 0.5f;
							float d = (new Vector2(cx, cy) - centerLogical).sqrMagnitude;
							float strength = d < sqrRadius ? Mathf.Pow(1f - d / sqrRadius, 2f) : 0f;
							quadData.Add((strength, qy * columns + qx));
						}

					quadData.Sort((a, b) => a.strength.CompareTo(b.strength));

					for (int drawIdx = 0; drawIdx < quadData.Count; drawIdx++)
					{
						var (strength, qIdx) = quadData[drawIdx];
						int qx = qIdx % columns;
						int qy = qIdx / columns;

						float x0 = qx; float y0 = qy;
						float x1 = x0 + 1f; float y1 = y0 + 1f;

						Vector2 quadCenter = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);

						float deltaScale = (!invalid && drawIdx == quadData.Count - 1) ? SELECTED_SIZE : 1f + strength;
						float transScale = strength * DELTA_TRANS_RATIO;

						int baseVert = drawIdx * 4;

						Vector2[] src = {
							new Vector2(x0, y0),
							new Vector2(x0, y1),
							new Vector2(x1, y1),
							new Vector2(x1, y0)
						};

						for (int i = 0; i < 4; i++)
						{
							var p = quadCenter + (src[i] - quadCenter) * deltaScale + (src[i] - centerLogical) * transScale;

							float scaledX = p.x * uvScaleX;
							float scaledY = p.y * uvScaleY;

							float finalX = contentOffsetX + scaledX * contentScaleX;
							float finalY = contentOffsetY + scaledY * contentScaleY;

							vertices[baseVert + i] = new Vector3(finalX, finalY, 0);
							uvs[baseVert + i] = new Vector2(src[i].x * uvScaleX, src[i].y * uvScaleY);

							float lum = 1f - strength * 0.75f;
							colors[baseVert + i] = new Color(lum, lum, lum, 1);
						}

						int tri = drawIdx * 6;
						indices[tri + 0] = baseVert + 0;
						indices[tri + 1] = baseVert + 1;
						indices[tri + 2] = baseVert + 2;
						indices[tri + 3] = baseVert + 0;
						indices[tri + 4] = baseVert + 2;
						indices[tri + 5] = baseVert + 3;
					}

					if (!invalid)
					{
						int lastBase = (quadData.Count - 1) * 4;
						colors[lastBase + 0] = colors[lastBase + 1] = colors[lastBase + 2] = colors[lastBase + 3] = Color.clear;

						// Remember last quad's starting vertex index instead of copying
						_selectedQuadVertexBase = lastBase;

						if (_selectedMesh == null) _selectedMesh = new Mesh();

						_selectedMesh.vertices = new[] { Vector3.zero, Vector3.up, Vector3.one, Vector3.right };
						_selectedMesh.uv = new[] { uvs[lastBase + 0], uvs[lastBase + 1], uvs[lastBase + 2], uvs[lastBase + 3] };
						_selectedMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
					}
					else
					{
						_selectedQuadVertexBase = -1;
					}

					_gridMesh.vertices = vertices;
					_gridMesh.uv = uvs;
					_gridMesh.colors = colors;
					_gridMesh.triangles = indices;

					var old = RenderTexture.active;
					RenderTexture.active = _mainRT;
					GL.Clear(true, true, Color.clear);

					GL.PushMatrix();
					GL.LoadOrtho();
					_renderMaterial.SetPass(0);
					Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);
					GL.PopMatrix();

					RenderTexture.active = old;

					// Selected RT rendering (unchanged)
					if (_selectedMesh == null) return;

					int selSize = Mathf.CeilToInt(cellSize * SELECTED_SIZE);

					if (_selectedRT == null || _selectedRT.width != selSize || _selectedRT.height != selSize)
					{
						_selectedRT?.Release();
						_selectedRT = new RenderTexture(selSize, selSize, 0, RenderTextureFormat.ARGB32)
						{
							filterMode = FilterMode.Bilinear
						};
						_selectedRT.Create();
					}

					old = RenderTexture.active;
					RenderTexture.active = _selectedRT;
					GL.Clear(true, true, Color.clear);

					GL.PushMatrix();
					GL.LoadOrtho();
					_renderMaterial.SetPass(0);
					Graphics.DrawMeshNow(_selectedMesh, Matrix4x4.identity);
					GL.PopMatrix();

					RenderTexture.active = old;
				}
			}

			void LazyInitResources()
			{
				if (_renderMaterial != null) return;

				var shader = Shader.Find("Sprites/Default");
				_renderMaterial = new Material(shader)
				{
					hideFlags = HideFlags.HideAndDontSave,
					mainTexture = atlas?.Texture ?? Texture2D.whiteTexture
				};
			}
		}
	}
}