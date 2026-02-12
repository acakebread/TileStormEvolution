using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ScreenSpaceUtil
	{
		private static Material _atlasMaterial;

		private static RenderTexture _gridRT;
		private static RenderTexture _focusRT;
		private static Mesh _gridMesh;
		private static Mesh _focusMesh;

		private static Vector2 _lastCoord = new Vector2(-999f, -999f);
		private static int _lastColumns = -1;
		private static int _lastRows = -1;
		private static int _lastCellSize = -1;

		private const float FOCUS_FACTOR = 5f;
		private const float DELTA_TRANS_RATIO = 0.375f;
		private const float FALL_OFF_RATIO = 0.625f;

		public static void OnGUI(IGridAtlas atlas, Rect rect, Vector2 coord = default)
		{
			if (atlas == null || atlas.Texture == null) return;

			if (coord == default) coord = new Vector2(0.5f, 0.5f);

			if (null == _atlasMaterial)
			{
				var shader = Shader.Find("Sprites/Default");
				_atlasMaterial = new Material(shader)
				{
					hideFlags = HideFlags.HideAndDontSave,
					mainTexture = atlas.Texture
				};
			}

			var columns = Mathf.Max(1, atlas.Columns);
			var rows = Mathf.Max(1, atlas.Rows);
			var cellSize = atlas.CellSize;
			var coreW = atlas.Texture.width;
			var coreH = atlas.Texture.height;

			var marginW = cellSize * 2;
			var marginH = cellSize * 2;

			var padW_temp = coreW + 2 * marginW;
			var padH_temp = coreH + 2 * marginH;

			// Round up each dimension independently
			int targetW = NextPowerOf2(padW_temp);
			int targetH = NextPowerOf2(padH_temp);

			// Re-calculate the actual margins needed to reach exactly the pow2 size
			marginW = (targetW - coreW) / 2;
			marginH = (targetH - coreH) / 2;

			// Now update padW / padH to the final (power-of-2) values the rest of the code expects
			var padW = targetW;
			var padH = targetH;

			var contentScaleX = (float)coreW / padW;
			var contentScaleY = (float)coreH / padH;
			var contentOffsetX = (float)marginW / padW;
			var contentOffsetY = (float)marginH / padH;

			var scaleW = (float)rect.width / coreW;
			var scaleH = (float)rect.height / coreH;
			marginW = (int)(marginW * scaleW);
			marginH = (int)(marginH * scaleH);

			var lastBase = (columns * rows - 1) * 4;

			if (_gridRT == null || _gridRT.width != padW || _gridRT.height != padH)
			{
				if (padW > 8192 || padH > 8192)
					Debug.LogError($"Maximum safe Render Texture size exceeded: {padW}:{padH}");
				else
					Debug.Log($"Render Texture size : {padW}:{padH}");

				_gridRT?.Release();
				_gridRT = new RenderTexture(padW, padH, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Bilinear
				};
				_gridRT.Create();
			}

			var invalid = coord.x < 0 || coord.x > 1 || coord.y < 0 || coord.y > 1;
			var coordChanged = (_lastCoord - coord).sqrMagnitude > 0.00000025f;

			var layoutChanged =
				_gridMesh == null ||
				coordChanged ||
				_lastColumns != columns ||
				_lastRows != rows ||
				_lastCellSize != cellSize;

			if (layoutChanged)
			{
				_lastColumns = columns;
				_lastRows = rows;
				_lastCellSize = cellSize;
				_lastCoord = coord;

				if (_gridMesh != null) Object.DestroyImmediate(_gridMesh);
				_gridMesh = new Mesh { name = $"DeformGrid_{columns}x{rows}" };

				if (invalid && _focusMesh != null)
				{
					Object.DestroyImmediate(_focusMesh);
					_focusMesh = null;
				}

				var totalCells = columns * rows;

				var vertices = new Vector3[totalCells * 4];
				var uvs = new Vector2[totalCells * 4];
				var colors = new Color[totalCells * 4];
				var indices = new int[totalCells * 6];

				Vector2 centerLogical = new Vector2(coord.x * columns, coord.y * rows);

				var uvScaleX = 1f / columns;
				var uvScaleY = 1f / rows;
				var falloffRadius = FALL_OFF_RATIO / uvScaleY;
				var sqrRadius = falloffRadius * falloffRadius;

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

				int quadCount = invalid ? quadData.Count : quadData.Count - 1;

				for (int drawIdx = 0; drawIdx < quadCount; drawIdx++)
				{
					var (strength, qIdx) = quadData[drawIdx];
					var x = (float)(qIdx % columns);
					var y = (float)(qIdx / columns);

					Vector2[] src = { new(x, y), new(x, y + 1f), new(x + 1f, y + 1f), new(x + 1f, y) };
					Vector2 quadCenter = new Vector2(x + 0.5f, y + 0.5f);

					float deltaScale = 1f + strength;
					float transScale = strength * DELTA_TRANS_RATIO;

					int baseVert = drawIdx * 4;

					var scaleX = uvScaleX * contentScaleX;
					var scaleY = uvScaleY * contentScaleY;
					var transX = contentOffsetX;
					var transY = contentOffsetY;

					float lum = 1f - strength * 0.75f;
					var colour = new Color(lum, lum, lum, 1);

					BuildQuad(vertices, uvs, colors, indices, baseVert, quadCenter, src, deltaScale, transScale, centerLogical,
						scaleX, scaleY, transX, transY, colour, uvScaleX, uvScaleY);
				}

				if (!invalid)
				{
					var (strength, qIdx) = quadData[^1];
					var x = (float)(qIdx % columns);
					var y = (float)(qIdx / columns);

					Vector2[] src = { new(x, y), new(x, y + 1f), new(x + 1f, y + 1f), new(x + 1f, y) };
					Vector2 quadCenter = new Vector2(x + 0.5f, y + 0.5f);

					float deltaScale = FOCUS_FACTOR;
					float transScale = strength * DELTA_TRANS_RATIO;

					int baseVert = quadCount * 4;

					var scaleX = uvScaleX * rect.width;
					var scaleY = uvScaleY * rect.height;
					var transX = rect.x;
					var transY = rect.y;

					BuildQuad(vertices, uvs, colors, indices, baseVert, quadCenter, src, deltaScale, transScale, centerLogical,
						scaleX, scaleY, transX, transY, Color.clear, uvScaleX, uvScaleY);
				}

				_gridMesh.vertices = vertices;
				_gridMesh.uv = uvs;
				_gridMesh.colors = colors;
				_gridMesh.triangles = indices;

				var old = RenderTexture.active;
				RenderTexture.active = _gridRT;
				GL.Clear(true, true, Color.clear);

				GL.PushMatrix();
				GL.LoadOrtho();
				_atlasMaterial.SetPass(0);
				Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);
				GL.PopMatrix();

				if (!invalid)
				{
					int selSize = Mathf.CeilToInt(cellSize * FOCUS_FACTOR);

					if (_focusRT == null || _focusRT.width != selSize || _focusRT.height != selSize)
					{
						_focusRT?.Release();
						_focusRT = new RenderTexture(selSize, selSize, 0, RenderTextureFormat.ARGB32)
						{
							filterMode = FilterMode.Bilinear
						};
						_focusRT.Create();
					}

					RenderTexture.active = _focusRT;
					GL.Clear(true, true, Color.clear);

					GL.PushMatrix();
					GL.LoadOrtho();
					_atlasMaterial.SetPass(0);

					if (_focusMesh == null) _focusMesh = new Mesh();

					var lastBaseForUV = (columns * rows - 1) * 4;
					_focusMesh.vertices = new[] { Vector3.zero, Vector3.up, Vector3.one, Vector3.right };
					_focusMesh.uv = new[] { uvs[lastBaseForUV + 0], uvs[lastBaseForUV + 1], uvs[lastBaseForUV + 2], uvs[lastBaseForUV + 3] };
					_focusMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
					_focusMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };

					Graphics.DrawMeshNow(_focusMesh, Matrix4x4.identity);
					GL.PopMatrix();
				}

				RenderTexture.active = old;
			}

			if (_gridRT == null) return;

			var displayRect = new Rect(rect.x - marginW, rect.y - marginH, rect.width + marginW * 2, rect.height + marginH * 2);
			GUI.DrawTexture(displayRect.ToGUIRect(), _gridRT, ScaleMode.ScaleToFit, true);

			if (_focusRT == null || _gridMesh == null || invalid) return;

			var verts = _gridMesh.vertices;
			var bl = verts[lastBase + 0];
			var tr = verts[lastBase + 2];

			var quadWidth = (tr.x - bl.x);
			var quadHeight = (tr.y - bl.y);

			var centerX = (bl.x + tr.x) * 0.5f;
			var centerY = (bl.y + tr.y) * 0.5f;

			var selRect = new Rect(centerX - quadWidth * 0.5f, centerY - quadHeight * 0.5f, quadWidth, quadHeight);
			GUI.DrawTexture(selRect.ToGUIRect(), _focusRT, ScaleMode.ScaleToFit, true);
		}

		private static void BuildQuad(
			Vector3[] vertices, Vector2[] uvs, Color[] colors, int[] indices,
			int baseVert, Vector2 quadCenter, Vector2[] src,
			float deltaScale, float transScale, Vector2 centerLogical,
			float scaleX, float scaleY, float transX, float transY,
			Color colour, float uvScaleX, float uvScaleY)
		{
			for (int i = 0; i < 4; i++)
			{
				var p = quadCenter + (src[i] - quadCenter) * deltaScale + (src[i] - centerLogical) * transScale;

				var finalX = p.x * scaleX + transX;
				var finalY = p.y * scaleY + transY;

				vertices[baseVert + i] = new Vector3(finalX, finalY, 0);
				uvs[baseVert + i] = new Vector2(src[i].x * uvScaleX, src[i].y * uvScaleY);
				colors[baseVert + i] = colour;
			}

			int tri = (baseVert / 4) * 6;
			indices[tri + 0] = baseVert + 0;
			indices[tri + 1] = baseVert + 1;
			indices[tri + 2] = baseVert + 2;
			indices[tri + 3] = baseVert + 0;
			indices[tri + 4] = baseVert + 2;
			indices[tri + 5] = baseVert + 3;
		}

		private static int NextPowerOf2(int x)
		{
			if (x <= 0) return 1;
			return Mathf.NextPowerOfTwo(x);
		}
	}
}
