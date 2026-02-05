using UnityEngine;

namespace MassiveHadronLtd
{
	public static class ScreenSpaceUtil
	{
		private static RenderTexture _rt;
		private static Mesh _quadMesh;
		private static Material _quadMaterial;

		private static bool _initialized;
		private static int _currentWidth = -1;
		private static int _currentHeight = -1;

		private static void EnsureRenderTexture(int targetWidth, int targetHeight)
		{
			if (_rt != null &&
				_rt.width == targetWidth &&
				_rt.height == targetHeight &&
				_rt.IsCreated())
			{
				return; // already good
			}

			// Release old one if it exists
			if (_rt != null)
			{
				_rt.Release();
				_rt = null;
			}

			Debug.Log($"[ScreenSpaceUtil] Creating/Resizing RT → {targetWidth}×{targetHeight}");

			_rt = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
			{
				name = "ScreenQuadRT",
				filterMode = FilterMode.Bilinear,
				antiAliasing = 1,
				useDynamicScale = false
			};
			_rt.Create();

			_currentWidth = targetWidth;
			_currentHeight = targetHeight;

			// Build quad mesh only once
			if (_quadMesh == null)
			{
				_quadMesh = new Mesh { name = "ScreenQuad" };
				_quadMesh.vertices = new Vector3[]
				{
					new(0, 0, 0),
					new(0, 1, 0),
					new(1, 1, 0),
					new(1, 0, 0)
				};
				_quadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
				_quadMesh.uv = new Vector2[]
				{
					new(0, 0),
					new(0, 1),
					new(1, 1),
					new(1, 0)
				};
			}

			// Material only once
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

				_quadMaterial = new Material(shader)
				{
					color = Color.white,
					mainTexture = TextureUtils.GenerateXorTexture256() // ← your placeholder
				};
			}

			_initialized = true;
		}

		/// <summary>
		/// Draws the quad immediately into the RT of the requested size.
		/// Recreates RT only if dimensions changed.
		/// </summary>
		public static RenderTexture GetRenderTexture(int w, int h)
		{
			if (w <= 0 || h <= 0)
			{
				Debug.LogWarning($"[ScreenSpaceUtil] Invalid size {w}×{h} — using fallback 256×256");
				w = 256;
				h = 256;
			}

			EnsureRenderTexture(w, h);

			if (_rt == null) return null;

			var oldRT = RenderTexture.active;
			RenderTexture.active = _rt;

			// Optional clear (uncomment if you want fresh frame each time)
			// GL.Clear(true, true, new Color(0,0,0,0));

			GL.PushMatrix();
			GL.LoadOrtho();

			_quadMaterial.SetPass(0);
			Graphics.DrawMeshNow(_quadMesh, Matrix4x4.identity);

			GL.PopMatrix();

			RenderTexture.active = oldRT;

			return _rt;
		}

		/// <summary>
		/// Full cleanup — call from OnDestroy / OnApplicationQuit etc.
		/// </summary>
		public static void Cleanup()
		{
			if (_rt != null)
			{
				_rt.Release();
				_rt = null;
			}

			if (_quadMesh != null)
			{
				Object.DestroyImmediate(_quadMesh);
				_quadMesh = null;
			}

			if (_quadMaterial != null)
			{
				Object.DestroyImmediate(_quadMaterial);
				_quadMaterial = null;
			}

			_initialized = false;
			_currentWidth = -1;
			_currentHeight = -1;
		}
	}
}