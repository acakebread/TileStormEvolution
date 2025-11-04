#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MassiveHadronLtd
{
	[InitializeOnLoad]
	public static class ParticleControllerSceneView
	{
		public static void OnRender(SceneView sceneView)
		{
			if (!Application.isPlaying && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
			{
				// Optional: update particles in edit mode
				foreach (var controller in Object.FindObjectsByType<ParticleController>(FindObjectsSortMode.None))
				{
					if (controller != null && controller.updateParticles && controller.customParticleSystem != null)
					{
						controller.customParticleSystem.UpdateParticles();
					}
				}
			}

			// Draw all controllers that want to be shown
			foreach (var controller in Object.FindObjectsByType<ParticleController>(FindObjectsSortMode.None))
			{
				if (controller == null || !controller.showInSceneView || controller.customParticleSystem == null)
					continue;

				var mesh = controller.customParticleSystem.GetDebugMesh();
				if (mesh == null) continue;

				var mat = controller.useDebugMaterial
					? GetCyanDebugMaterial()
					: controller.ParticleMaterial;

				if (mat == null) continue;

				// Force white vertex colors for debug material (so tint is ignored)
				if (controller.useDebugMaterial)
					EnsureWhiteColors(mesh);

				mat.SetPass(0);
				Graphics.DrawMeshNow(mesh, controller.transform.localToWorldMatrix);
			}
		}

		private static Material _cyanDebugMaterial;
		private static Color[] _whiteColors;
		private static int _lastVertexCount;

		private static Material GetCyanDebugMaterial()
		{
			if (_cyanDebugMaterial != null) return _cyanDebugMaterial;

			var shader = Shader.Find("Debug/TriggerWireframe");
			if (shader == null)
			{
				Debug.LogWarning("Debug/TriggerWireframe not found. Using Unlit/Color.");
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

		// Optional: cleanup
		[UnityEditor.Callbacks.DidReloadScripts]
		private static void OnScriptsReloaded()
		{
			if (_cyanDebugMaterial != null)
			{
				Object.DestroyImmediate(_cyanDebugMaterial);
				_cyanDebugMaterial = null;
			}
		}
	}
}
#endif