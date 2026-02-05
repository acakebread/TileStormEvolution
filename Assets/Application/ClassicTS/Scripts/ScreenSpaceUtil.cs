using UnityEngine;

namespace MassiveHadronLtd
{
	public static class ScreenSpaceUtil
	{
		private static RenderTexture _rt;
		private static Mesh _gridMesh;          // now one combined mesh
		private static Material _quadMaterial;
		private static Texture2D _xorTexture;   // supplied externally

		// We'll rebuild the mesh only when cell count changes
		private static int _lastColumns = -1;
		private static int _lastRows = -1;

		private static void LazyInitMaterialAndTexture()
		{
			if (_quadMaterial == null)
			{
				var shader = Shader.Find("Universal Render Pipeline/Unlit")
						   ?? Shader.Find("Unlit/Color")
						   ?? Shader.Find("Unlit/Texture");

				if (shader == null)
				{
					Debug.LogError("[ScreenSpaceUtil] No compatible Unlit shader found!");
					return;
				}

				_quadMaterial = new Material(shader);
			}

			// _xorTexture must be assigned externally before first use
			// We no longer generate or modify it here

			if (_xorTexture == null)
				_xorTexture = TextureUtils.GenerateXorTexture256(); // assuming this returns Texture2D
		}

		private static void RebuildGridMeshIfNeeded(int numColumns, int numRows)
		{
			if (numColumns < 1) numColumns = 1;
			if (numRows < 1) numRows = 1;

			if (_gridMesh != null &&
				_lastColumns == numColumns &&
				_lastRows == numRows)
			{
				return; // mesh is already correct
			}

			Debug.Log($"[ScreenSpaceUtil] Rebuilding grid mesh for {numColumns}×{numRows} cells");

			if (_gridMesh != null)
			{
				Object.DestroyImmediate(_gridMesh);
			}

			_gridMesh = new Mesh { name = $"GridMesh_{numColumns}x{numRows}" };

			int cellCount = numColumns * numRows;

			Vector3[] vertices = new Vector3[cellCount * 4];
			Vector2[] uvs = new Vector2[cellCount * 4];
			int[] indices = new int[cellCount * 6];

			float cellW = 1f / numColumns;  // normalized ortho space (0..1)
			float cellH = 1f / numRows;

			int vIdx = 0;
			int iIdx = 0;

			for (int y = 0; y < numRows; y++)
			{
				for (int x = 0; x < numColumns; x++)
				{
					float left = x * cellW;
					float bottom = y * cellH;
					float right = left + cellW;
					float top = bottom + cellH;

					// Bottom-left, top-left, top-right, bottom-right
					vertices[vIdx + 0] = new Vector3(left, bottom, 0);
					vertices[vIdx + 1] = new Vector3(left, top, 0);
					vertices[vIdx + 2] = new Vector3(right, top, 0);
					vertices[vIdx + 3] = new Vector3(right, bottom, 0);

					// UVs always full 0..1 on the XOR texture (per cell)
					uvs[vIdx + 0] = new Vector2(0, 0);
					uvs[vIdx + 1] = new Vector2(0, 1);
					uvs[vIdx + 2] = new Vector2(1, 1);
					uvs[vIdx + 3] = new Vector2(1, 0);

					// Two triangles: 0-1-2 and 0-2-3
					indices[iIdx + 0] = vIdx + 0;
					indices[iIdx + 1] = vIdx + 1;
					indices[iIdx + 2] = vIdx + 2;
					indices[iIdx + 3] = vIdx + 0;
					indices[iIdx + 4] = vIdx + 2;
					indices[iIdx + 5] = vIdx + 3;

					vIdx += 4;
					iIdx += 6;
				}
			}

			_gridMesh.vertices = vertices;
			_gridMesh.uv = uvs;
			_gridMesh.triangles = indices;

			// No need for normals/bounds recalc for simple ortho unlit quad grid

			_lastColumns = numColumns;
			_lastRows = numRows;
		}

		private static void DrawGridToRT(int numColumns, int numRows)
		{
			int pixelWidth = numColumns * 64;
			int pixelHeight = numRows * 64;

			// Recreate RT only if size changed
			if (_rt == null || _rt.width != pixelWidth || _rt.height != pixelHeight || !_rt.IsCreated())
			{
				if (_rt != null)
				{
					_rt.Release();
					_rt = null;
				}

				Debug.Log($"[ScreenSpaceUtil] Creating grid RT → {pixelWidth}×{pixelHeight}");

				_rt = new RenderTexture(pixelWidth, pixelHeight, 0, RenderTextureFormat.ARGB32)
				{
					name = $"GridRT_{numColumns}x{numRows}",
					filterMode = FilterMode.Point,
					antiAliasing = 1,
					useDynamicScale = false
				};
				_rt.Create();
			}

			LazyInitMaterialAndTexture();
			if (_rt == null || _quadMaterial == null || _xorTexture == null) return;

			RebuildGridMeshIfNeeded(numColumns, numRows);
			if (_gridMesh == null) return;

			var oldRT = RenderTexture.active;
			RenderTexture.active = _rt;

			GL.Clear(true, true, new Color(0, 0, 0, 0));

			GL.PushMatrix();
			GL.LoadOrtho();

			// Full grid uses one material + one draw call
			_quadMaterial.mainTexture = _xorTexture;
			_quadMaterial.color = Color.white;           // base white – tint per-quad not needed here
			_quadMaterial.SetPass(0);

			Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);

			GL.PopMatrix();

			RenderTexture.active = oldRT;
		}

		/// <summary>
		/// Returns RT with numColumns × numRows grid of 64×64 XOR tiles.
		/// Expects _xorTexture to be assigned externally beforehand.
		/// </summary>
		public static RenderTexture GetRenderTexture(int numColumns = 8, int numRows = 8)
		{
			DrawGridToRT(numColumns, numRows);
			return _rt;
		}

		public static void Cleanup()
		{
			if (_rt != null)
			{
				_rt.Release();
				_rt = null;
			}

			if (_gridMesh != null)
			{
				Object.DestroyImmediate(_gridMesh);
				_gridMesh = null;
			}

			if (_quadMaterial != null)
			{
				Object.DestroyImmediate(_quadMaterial);
				_quadMaterial = null;
			}

			// Do NOT destroy _xorTexture — it's externally owned
			_xorTexture = null;

			_lastColumns = -1;
			_lastRows = -1;
		}

		// Optional: helper to assign texture from outside
		public static void SetXorTexture(Texture2D tex)
		{
			_xorTexture = tex;
		}
	}
}

//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public static class ScreenSpaceUtil
//	{
//		private static RenderTexture _rt;
//		private static Mesh _quadMesh;
//		private static Material _quadMaterial;

//		private static Texture2D _xorTexture;  // cached once

//		private static void LazyInitResources()
//		{
//			if (_quadMesh == null)
//			{
//				_quadMesh = new Mesh { name = "UnitQuad" };
//				_quadMesh.vertices = new Vector3[]
//				{
//					new(0, 0, 0),
//					new(0, 1, 0),
//					new(1, 1, 0),
//					new(1, 0, 0)
//				};
//				_quadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
//				_quadMesh.uv = new Vector2[]
//				{
//					new(0, 0),
//					new(0, 1),
//					new(1, 1),
//					new(1, 0)
//				};
//			}

//			if (_quadMaterial == null)
//			{
//				var shader = Shader.Find("Universal Render Pipeline/Unlit")
//						   ?? Shader.Find("Unlit/Color")
//						   ?? Shader.Find("Unlit/Texture");

//				if (shader == null)
//				{
//					Debug.LogError("[ScreenSpaceUtil] No compatible Unlit shader found!");
//					return;
//				}

//				_quadMaterial = new Material(shader);
//			}

//			if (_xorTexture == null)
//			{
//				_xorTexture = TextureUtils.GenerateXorTexture256(); // assuming this returns Texture2D
//				//if (_xorTexture != null)
//				//{
//				//	_xorTexture.filterMode = FilterMode.Point; // crisp XOR pattern
//				//	_xorTexture.Apply(false);
//				//}
//			}
//		}

//		private static void DrawGridToRT(int numColumns, int numRows)
//		{
//			if (numColumns < 1) numColumns = 1;
//			if (numRows < 1) numRows = 1;

//			int pixelWidth = numColumns * 64;
//			int pixelHeight = numRows * 64;

//			// Recreate RT if size mismatch or not created
//			if (_rt == null || _rt.width != pixelWidth || _rt.height != pixelHeight || !_rt.IsCreated())
//			{
//				if (_rt != null)
//				{
//					_rt.Release();
//					_rt = null;
//				}

//				Debug.Log($"[ScreenSpaceUtil] Creating grid RT → {pixelWidth}×{pixelHeight} ({numColumns}×{numRows} cells)");

//				_rt = new RenderTexture(pixelWidth, pixelHeight, 0, RenderTextureFormat.ARGB32)
//				{
//					name = $"GridRT_{numColumns}x{numRows}",
//					filterMode = FilterMode.Point,     // keep crisp 64×64 blocks
//					antiAliasing = 1,
//					useDynamicScale = false
//				};
//				_rt.Create();
//			}

//			// Prepare resources
//			LazyInitResources();
//			if (_rt == null || _xorTexture == null || _quadMaterial == null) return;

//			var oldRT = RenderTexture.active;
//			RenderTexture.active = _rt;

//			// Clear to black / transparent (your choice)
//			GL.Clear(true, true, new Color(0, 0, 0, 0));

//			GL.PushMatrix();
//			GL.LoadOrtho();             // 0..1 normalized device coords

//			float cellW = 1f / numColumns;  // in ortho space
//			float cellH = 1f / numRows;

//			_quadMaterial.mainTexture = _xorTexture;

//			for (int y = 0; y < numRows; y++)
//			{
//				for (int x = 0; x < numColumns; x++)
//				{
//					// Checkerboard: full vs 50% brightness
//					bool bright = ((x + y) % 2) == 0;
//					Color tint = bright ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);

//					_quadMaterial.color = tint;
//					_quadMaterial.SetPass(0);

//					// Position this quad in ortho space
//					Matrix4x4 matrix = Matrix4x4.TRS(
//						new Vector3(x * cellW, y * cellH, 0),
//						Quaternion.identity,
//						new Vector3(cellW, cellH, 1)
//					);

//					Graphics.DrawMeshNow(_quadMesh, matrix);
//				}
//			}

//			GL.PopMatrix();

//			RenderTexture.active = oldRT;
//		}

//		/// <summary>
//		/// Returns a RenderTexture containing a numColumns × numRows grid of 64×64 XOR tiles,
//		/// with checkerboard brightness modulation ((x+y) % 2).
//		/// </summary>
//		public static RenderTexture GetRenderTexture(int numColumns = 8, int numRows = 8)
//		{
//			DrawGridToRT(numColumns, numRows);
//			return _rt;
//		}

//		/// <summary>
//		/// Clean up all static resources (call from OnDestroy / OnApplicationQuit etc.)
//		/// </summary>
//		public static void Cleanup()
//		{
//			if (_rt != null)
//			{
//				_rt.Release();
//				_rt = null;
//			}

//			if (_quadMesh != null)
//			{
//				Object.DestroyImmediate(_quadMesh);
//				_quadMesh = null;
//			}

//			if (_quadMaterial != null)
//			{
//				Object.DestroyImmediate(_quadMaterial);
//				_quadMaterial = null;
//			}

//			if (_xorTexture != null)
//			{
//				// Usually don't destroy asset-like textures, but if it's generated → destroy
//				// Object.DestroyImmediate(_xorTexture);
//				_xorTexture = null;
//			}
//		}
//	}
//}

//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public static class ScreenSpaceUtil
//	{
//		private static RenderTexture _rt;
//		private static Mesh _quadMesh;
//		private static Material _quadMaterial;

//		private static bool _initialized;
//		private static int _currentWidth = -1;
//		private static int _currentHeight = -1;

//		private static void EnsureRenderTexture(int targetWidth, int targetHeight)
//		{
//			if (_rt != null &&
//				_rt.width == targetWidth &&
//				_rt.height == targetHeight &&
//				_rt.IsCreated())
//			{
//				return; // already good
//			}

//			// Release old one if it exists
//			if (_rt != null)
//			{
//				_rt.Release();
//				_rt = null;
//			}

//			Debug.Log($"[ScreenSpaceUtil] Creating/Resizing RT → {targetWidth}×{targetHeight}");

//			_rt = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
//			{
//				name = "ScreenQuadRT",
//				filterMode = FilterMode.Bilinear,
//				antiAliasing = 1,
//				useDynamicScale = false
//			};
//			_rt.Create();

//			_currentWidth = targetWidth;
//			_currentHeight = targetHeight;

//			// Build quad mesh only once
//			if (_quadMesh == null)
//			{
//				_quadMesh = new Mesh { name = "ScreenQuad" };
//				_quadMesh.vertices = new Vector3[]
//				{
//					new(0, 0, 0),
//					new(0, 1, 0),
//					new(1, 1, 0),
//					new(1, 0, 0)
//				};
//				_quadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
//				_quadMesh.uv = new Vector2[]
//				{
//					new(0, 0),
//					new(0, 1),
//					new(1, 1),
//					new(1, 0)
//				};
//			}

//			// Material only once
//			if (_quadMaterial == null)
//			{
//				var shader = Shader.Find("Universal Render Pipeline/Unlit")
//						   ?? Shader.Find("Unlit/Color")
//						   ?? Shader.Find("Unlit/Texture");

//				if (shader == null)
//				{
//					Debug.LogError("[ScreenSpaceUtil] No compatible Unlit shader found!");
//					return;
//				}

//				_quadMaterial = new Material(shader)
//				{
//					color = Color.white,
//					mainTexture = TextureUtils.GenerateXorTexture256() // ← your placeholder
//				};
//			}

//			_initialized = true;
//		}

//		/// <summary>
//		/// Draws the quad immediately into the RT of the requested size.
//		/// Recreates RT only if dimensions changed.
//		/// </summary>
//		public static RenderTexture GetRenderTexture(int w, int h)
//		{
//			if (w <= 0 || h <= 0)
//			{
//				Debug.LogWarning($"[ScreenSpaceUtil] Invalid size {w}×{h} — using fallback 256×256");
//				w = 256;
//				h = 256;
//			}

//			EnsureRenderTexture(w, h);

//			if (_rt == null) return null;

//			var oldRT = RenderTexture.active;
//			RenderTexture.active = _rt;

//			// Optional clear (uncomment if you want fresh frame each time)
//			// GL.Clear(true, true, new Color(0,0,0,0));

//			GL.PushMatrix();
//			GL.LoadOrtho();

//			_quadMaterial.SetPass(0);
//			Graphics.DrawMeshNow(_quadMesh, Matrix4x4.identity);

//			GL.PopMatrix();

//			RenderTexture.active = oldRT;

//			return _rt;
//		}

//		/// <summary>
//		/// Full cleanup — call from OnDestroy / OnApplicationQuit etc.
//		/// </summary>
//		public static void Cleanup()
//		{
//			if (_rt != null)
//			{
//				_rt.Release();
//				_rt = null;
//			}

//			if (_quadMesh != null)
//			{
//				Object.DestroyImmediate(_quadMesh);
//				_quadMesh = null;
//			}

//			if (_quadMaterial != null)
//			{
//				Object.DestroyImmediate(_quadMaterial);
//				_quadMaterial = null;
//			}

//			_initialized = false;
//			_currentWidth = -1;
//			_currentHeight = -1;
//		}
//	}
//}