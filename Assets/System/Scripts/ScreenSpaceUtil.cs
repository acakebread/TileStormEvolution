using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ScreenSpaceUtil
	{
		private static RenderTexture _mainRT;
		private static RenderTexture _selectedRT;

		private static Mesh _gridMesh;
		private static Mesh _selectedQuadMesh;

		private static Material _quadMaterial;

		private static Vector3[] _selectedQuadMeshverts;

		private static Vector2 _lastCoord = new Vector2(-999f, -999f);

		private static bool _gridDirty = true;
		private static bool _selectedDirty = true;

		private const float SELECTED_SIZE = 4f;
		private const float DELTA_TRANS_RATIO = 0.375f;

		// Padding added around the icon grid when rendering to the RT
		// (not part of the atlas itself)
		public const int BORDER = 256;

		private static IGridIconAtlas _currentAtlas;

		//public static void SetAtlas(IGridIconAtlas atlas)
		//{
		//	_currentAtlas = atlas;
		//	_gridDirty = _selectedDirty = true;
		//}

		//public static IGridIconAtlas GetAtlas() => _currentAtlas;

		// ─────────────────────────────────────────────────────────────

		private static void RebuildGridMeshIfNeeded(Vector2 point)
		{
			if (_currentAtlas == null || _currentAtlas.Texture == null)
			{
				_gridMesh = null;
				_selectedQuadMeshverts = null;
				return;
			}

			int columns = _currentAtlas.Columns;
			int rows = _currentAtlas.Rows;
			int iconSize = _currentAtlas.CellSize;

			if (columns < 1) columns = 1;
			if (rows < 1) rows = 1;

			bool coordChanged = (_lastCoord - point).sqrMagnitude > 0.00000025f;

			bool needsRebuild = _gridMesh == null ||
								coordChanged;

			if (!needsRebuild) return;

			_gridDirty = _selectedDirty = true;

			if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);
			_gridMesh = new Mesh { name = $"DeformGrid_{columns}x{rows}" };

			bool invalid = point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1;

			if (invalid)
			{
				_selectedQuadMeshverts = null;

				if (_selectedQuadMesh != null)
				{
					Object.DestroyImmediate(_selectedQuadMesh);
					_selectedQuadMesh = null;
				}
			}

			int corePixelWidth = columns * iconSize;
			int corePixelHeight = rows * iconSize;
			int paddedPixelWidth = corePixelWidth + 2 * BORDER;
			int paddedPixelHeight = corePixelHeight + 2 * BORDER;

			float contentScaleX = (float)corePixelWidth / paddedPixelWidth;
			float contentScaleY = (float)corePixelHeight / paddedPixelHeight;
			float contentOffsetX = (float)BORDER / paddedPixelWidth;
			float contentOffsetY = (float)BORDER / paddedPixelHeight;

			int totalCells = columns * rows;
			var vertices = new Vector3[totalCells * 4];
			var uvs = new Vector2[totalCells * 4];
			var colors = new Color[totalCells * 4];
			var indices = new int[totalCells * 6];

			Vector2 centerLogical = new Vector2(point.x * columns, point.y * rows);

			float uvScaleX = 1f / columns;
			float uvScaleY = 1f / rows;
			float falloffRadius = 0.625f / uvScaleY;
			float sqrRadius = falloffRadius * falloffRadius;

			var quadData = new List<(float scaleStrength, int qIdx)>(totalCells);

			for (int qy = 0; qy < rows; qy++)
				for (int qx = 0; qx < columns; qx++)
				{
					float x = qx + 0.5f;
					float y = qy + 0.5f;
					float d = (new Vector2(x, y) - centerLogical).sqrMagnitude;
					float scale = d < sqrRadius ? Mathf.Pow(1f - d / sqrRadius, 2f) : 0f;
					quadData.Add((scale, qy * columns + qx));
				}

			quadData.Sort((a, b) => a.scaleStrength.CompareTo(b.scaleStrength));

			for (int drawIdx = 0; drawIdx < quadData.Count; drawIdx++)
			{
				var (scaleStrength, qIdx) = quadData[drawIdx];
				int qx = qIdx % columns;
				int qy = qIdx / columns;

				float x0 = qx;
				float y0 = qy;
				float x1 = x0 + 1f;
				float y1 = y0 + 1f;

				Vector2 quadCenter = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);

				float deltaScale = (!invalid && drawIdx == quadData.Count - 1) ? SELECTED_SIZE : 1f + scaleStrength;
				float transScale = scaleStrength * DELTA_TRANS_RATIO;

				int baseVert = drawIdx * 4;

				Vector2[] src = { new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0) };

				for (int i = 0; i < 4; i++)
				{
					var p = quadCenter + (src[i] - quadCenter) * deltaScale + (src[i] - centerLogical) * transScale;

					float scaledX = p.x * uvScaleX;
					float scaledY = p.y * uvScaleY;

					float finalX = contentOffsetX + scaledX * contentScaleX;
					float finalY = contentOffsetY + scaledY * contentScaleY;

					vertices[baseVert + i] = new Vector3(finalX, finalY, 0);
					uvs[baseVert + i] = new Vector2(src[i].x * uvScaleX, src[i].y * uvScaleY);

					float luminance = 1f - scaleStrength * 0.75f;
					colors[baseVert + i] = new Color(luminance, luminance, luminance, 1);
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
			}

			_gridMesh.vertices = vertices;
			_gridMesh.uv = uvs;
			_gridMesh.colors = colors;
			_gridMesh.triangles = indices;

			_lastCoord = point;

			if (!invalid)
			{
				int lastBase = (quadData.Count - 1) * 4;

				_selectedQuadMeshverts = new[]
				{
					vertices[lastBase + 0],
					vertices[lastBase + 1],
					vertices[lastBase + 2],
					vertices[lastBase + 3]
				};

				if (_selectedQuadMesh == null)
					_selectedQuadMesh = new Mesh();

				_selectedQuadMesh.vertices = new[] { Vector3.zero, Vector3.up, Vector3.one, Vector3.right };
				_selectedQuadMesh.uv = new[] { uvs[lastBase + 0], uvs[lastBase + 1], uvs[lastBase + 2], uvs[lastBase + 3] };
				_selectedQuadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
			}
		}

		// ─────────────────────────────────────────────────────────────

		private static void DrawGridToRT(Vector2 coord)
		{
			LazyInitResources();
			RebuildGridMeshIfNeeded(coord);

			if (_currentAtlas == null) return;

			int columns = _currentAtlas.Columns;
			int rows = _currentAtlas.Rows;
			int iconSize = _currentAtlas.CellSize;

			int w = columns * iconSize + 2 * BORDER;
			int h = rows * iconSize + 2 * BORDER;

			if (_mainRT == null || _mainRT.width != w || _mainRT.height != h)
			{
				if (_mainRT != null) _mainRT.Release();
				_mainRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Bilinear
				};
				_mainRT.Create();
				_gridDirty = true;
			}

			if (!_gridDirty) return;
			_gridDirty = false;

			var old = RenderTexture.active;
			RenderTexture.active = _mainRT;
			GL.Clear(true, true, Color.clear);

			GL.PushMatrix();
			GL.LoadOrtho();
			_quadMaterial.SetPass(0);
			Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);
			GL.PopMatrix();

			RenderTexture.active = old;
		}

		private static void DrawSelectedOnlyToRT()
		{
			if (_selectedQuadMesh == null) return;

			int iconSize = _currentAtlas?.CellSize ?? 128;
			int selSize = Mathf.CeilToInt(iconSize * SELECTED_SIZE);

			if (_selectedRT == null || _selectedRT.width != selSize)
			{
				if (_selectedRT != null) _selectedRT.Release();
				_selectedRT = new RenderTexture(selSize, selSize, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Bilinear
				};
				_selectedRT.Create();
				_selectedDirty = true;
			}

			if (!_selectedDirty) return;
			_selectedDirty = false;

			var old = RenderTexture.active;
			RenderTexture.active = _selectedRT;
			GL.Clear(true, true, Color.clear);

			GL.PushMatrix();
			GL.LoadOrtho();
			_quadMaterial.SetPass(0);
			Graphics.DrawMeshNow(_selectedQuadMesh, Matrix4x4.identity);
			GL.PopMatrix();

			RenderTexture.active = old;
		}

		// ─────────────────────────────────────────────────────────────

		public static void OnGUI(IGridIconAtlas atlas, Rect rect, Vector2 coord = default)
		{
			_currentAtlas = atlas;

			if (coord == default) coord = new Vector2(0.5f, 0.5f);

			if (_currentAtlas == null || _currentAtlas.Texture == null) return;

			DrawGridToRT(coord);
			DrawSelectedOnlyToRT();

			if (_mainRT == null) return;

			float renderW = _mainRT.width - BORDER * 2;
			float renderH = _mainRT.height - BORDER * 2;

			float scaleW = rect.width / renderW;
			float scaleH = rect.height / renderH;

			float borderX = BORDER * scaleW;
			float borderY = BORDER * scaleH;

			var displayRect = new Rect(rect.x - borderX, rect.y - borderY, rect.width + borderX * 2, rect.height + borderY * 2);
			GUI.DrawTexture(displayRect.ToGUIRect(), _mainRT, ScaleMode.ScaleToFit, true);

			if (_selectedRT == null || _selectedQuadMeshverts == null) return;

			float quadWidth = (_selectedQuadMeshverts[2].x - _selectedQuadMeshverts[0].x) * _mainRT.width * scaleW;
			float quadHeight = (_selectedQuadMeshverts[1].y - _selectedQuadMeshverts[0].y) * _mainRT.height * scaleH;

			float selectedPosX = (_selectedQuadMeshverts[2].x + _selectedQuadMeshverts[0].x) * 0.5f * _mainRT.width * scaleW;
			float selectedPosY = (_selectedQuadMeshverts[1].y + _selectedQuadMeshverts[0].y) * 0.5f * _mainRT.height * scaleH;

			float centerX = selectedPosX - borderX + rect.x;
			float centerY = selectedPosY - borderY + rect.y;

			var selRect = new Rect(centerX - quadWidth * 0.5f, centerY - quadHeight * 0.5f, quadWidth, quadHeight);
			GUI.DrawTexture(selRect.ToGUIRect(), _selectedRT, ScaleMode.ScaleToFit, true);
		}

		// ─────────────────────────────────────────────────────────────

		private static void LazyInitResources()
		{
			if (_quadMaterial != null) return;

			var shader = Shader.Find("Sprites/Default");
			_quadMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

			_quadMaterial.mainTexture = _currentAtlas?.Texture ?? Texture2D.whiteTexture;
		}
	}
}