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
				// Use a shader that actually reads vertex colors
				var shader = Shader.Find("Sprites/Default");

				if (shader == null)
				{
					// Fallbacks if Sprites/Default is missing (rare, but...)
					shader = Shader.Find("Hidden/Internal-Colored")
						  ?? Shader.Find("Particles/Standard Unlit")
						  ?? Shader.Find("Universal Render Pipeline/Unlit");

					if (shader == null)
					{
						Debug.LogError("[ScreenSpaceUtil] No usable shader found! Check project has Sprites/Default or URP package.");
						return;
					}
				}

				_quadMaterial = new Material(shader);
			}

			if (_xorTexture == null)
			{
				_xorTexture = TextureUtils.GenerateXorTexture256();
				if (_xorTexture != null)
				{
					_xorTexture.filterMode = FilterMode.Point;
				}
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

			int cellCount = numColumns * numRows;
			int vertsPerCell = 4;
			int totalVerts = cellCount * vertsPerCell;
			int totalIndices = cellCount * 6;

			Vector3[] vertices = new Vector3[totalVerts];
			Vector2[] uvs = new Vector2[totalVerts];
			Color[] colors = new Color[totalVerts];
			int[] indices = new int[totalIndices];

			float cellW = 1f / numColumns;
			float cellH = 1f / numRows;

			const float falloffRadius = 0.25f;
			const float maxDisplacement = 0.2f;

			float conceptualAspect = (float)numColumns / (float)numRows;

			int vertIndex = 0;
			int indexIndex = 0;

			for (int qy = 0; qy < numRows; qy++)
			{
				for (int qx = 0; qx < numColumns; qx++)
				{
					float left = qx * cellW;
					float bottom = qy * cellH;
					float right = left + cellW;
					float top = bottom + cellH;

					// Four corners in ortho space
					Vector2[] basePos = {
						new Vector2(left, bottom),      // BL
						new Vector2(left, top),         // TL
						new Vector2(right, top),        // TR
						new Vector2(right, bottom)      // BR
					};

					// Checkerboard per cell (qx + qy)
					bool isBright = ((qx + qy) % 2) == 0;

					// TEMP DEBUG: force extreme contrast to prove pattern
					// Comment these out once confirmed
					//Color cellColor = isBright ? Color.green : Color.red;

					// Normal version (uncomment when debug is done)
					Color cellColor = isBright ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);

					int baseVert = vertIndex;

					for (int c = 0; c < 4; c++)
					{
						Vector2 pos = basePos[c];

						// Aspect-aware delta (makes circle round after StretchToFill)
						float aspect = (float)Screen.width / (float)Screen.height; // real screen aspect
						Vector2 delta = pos - center;
						delta.x *= aspect;  // correct horizontal distance

						float dist = delta.magnitude;

						// Direction in aspect-corrected space (so radial is symmetric on screen)
						Vector2 correctedDir = delta.normalized;
						if (dist < 1e-6f) correctedDir = Vector2.up; // safe fallback

						// Strength (half-cosine: max at center, zero at edge)
						float strength = 0f;
						if (dist < falloffRadius)
						{
							float t = dist / falloffRadius;
							strength = Mathf.Cos(Mathf.PI * t * 0.5f); // 1 → 0
						}

						// Now map corrected direction back to original space for displacement
						Vector2 dirOriginal = new Vector2(correctedDir.x / aspect, correctedDir.y);
						if (dirOriginal.sqrMagnitude < 1e-6f) dirOriginal = Vector2.up;

						Vector2 offset = dirOriginal * (maxDisplacement * strength);

						vertices[vertIndex] = new Vector3(pos.x + offset.x, pos.y + offset.y, 0f);
						uvs[vertIndex] = new Vector2(pos.x, pos.y);
						colors[vertIndex] = cellColor;

						vertIndex++;
					}

					// Indices
					indices[indexIndex++] = baseVert + 0;
					indices[indexIndex++] = baseVert + 1;
					indices[indexIndex++] = baseVert + 2;

					indices[indexIndex++] = baseVert + 0;
					indices[indexIndex++] = baseVert + 2;
					indices[indexIndex++] = baseVert + 3;
				}
			}

			_gridMesh.vertices = vertices;
			_gridMesh.uv = uvs;
			_gridMesh.colors = colors;
			_gridMesh.triangles = indices;

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
			_quadMaterial.color = Color.white;          // base white so vertex colors can darken
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