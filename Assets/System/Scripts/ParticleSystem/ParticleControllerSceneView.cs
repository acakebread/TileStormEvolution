#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MassiveHadronLtd
{
	[InitializeOnLoad]
	public static class ParticleControllerSceneView
	{
		private static Material _cyanDebugMaterial;
		private static Color[] _whiteColors;
		private static int _lastVertexCount;

		static ParticleControllerSceneView()
		{
			// Cleanup on domain reload (play mode exit, script reload, etc.)
			AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
		}

		public static void OnRender(ParticleController controller)
		{
			var mesh = controller.customParticleSystem.GetDebugMesh();
			if (mesh == null) return;

			var mat = controller.useDebugMaterial ? GetCyanDebugMaterial() : controller.ParticleMaterial;
			if (mat == null) return;

			if (controller.useDebugMaterial)
			{
				EnsureWhiteColors(mesh);
			}

			mat.SetPass(0);
			Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
		}

		private static Material GetCyanDebugMaterial()
		{
			if (_cyanDebugMaterial != null) return _cyanDebugMaterial;

			var shader = Shader.Find("Hidden/Internal-Colored");
			if (shader == null)
			{
				Debug.LogWarning("Hidden/Internal-Colored not found. Using Unlit/Color.");
				shader = Shader.Find("Unlit/Color");
			}

			_cyanDebugMaterial = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave,
				color = new Color(0f, 1f, 1f, 0.3f)
			};

			return _cyanDebugMaterial;
		}

		private static void EnsureWhiteColors(Mesh mesh)
		{
			int count = mesh.vertexCount;
			if (_whiteColors == null || _whiteColors.Length != count)
			{
				_whiteColors = new Color[count];
				for (int i = 0; i < count; i++)
					_whiteColors[i] = Color.white;
				_lastVertexCount = count;
			}
			else if (_lastVertexCount != count)
			{
				System.Array.Resize(ref _whiteColors, count);
				for (int i = _lastVertexCount; i < count; i++)
					_whiteColors[i] = Color.white;
				_lastVertexCount = count;
			}

			mesh.colors = _whiteColors;
		}

		// Reinstate OnDestroy behavior via AssemblyReloadEvents
		[UnityEditor.Callbacks.DidReloadScripts]
		private static void OnAfterAssemblyReload()
		{
			Cleanup();
		}

		// Also clean up when entering play mode
		private static void Cleanup()
		{
			if (_cyanDebugMaterial != null)
			{
				Object.DestroyImmediate(_cyanDebugMaterial);
				_cyanDebugMaterial = null;
			}
			_whiteColors = null;
			_lastVertexCount = 0;
		}

		// Optional: Clean up on play mode change
		[InitializeOnLoadMethod]
		private static void SetupPlayModeCleanup()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.EnteredEditMode)
			{
				Cleanup();
			}
		}
	}
}
#endif