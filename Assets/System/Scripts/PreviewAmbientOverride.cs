using UnityEngine;
using UnityEngine.Rendering;

namespace ClassicTilestorm // or wherever fits
{
	[RequireComponent(typeof(Camera))]
	public class PreviewAmbientOverride : MonoBehaviour
	{
		[SerializeField] private Map mapToUse; // assign in inspector or via code

		private SphericalHarmonicsL2 originalProbe;
		private Color originalAmbientLight;
		private AmbientMode originalMode;
		private float originalIntensity;
		private Material originalSkybox;

		private void OnEnable()
		{
			RenderPipelineManager.beginCameraRendering += OnBeginRender;
			RenderPipelineManager.endCameraRendering += OnEndRender;
		}

		private void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= OnBeginRender;
			RenderPipelineManager.endCameraRendering -= OnEndRender;
		}

		private void OnBeginRender(ScriptableRenderContext context, Camera cam)
		{
			if (cam != GetComponent<Camera>()) return;

			// Save original settings
			originalAmbientLight = RenderSettings.ambientLight;
			originalMode = RenderSettings.ambientMode;
			originalIntensity = RenderSettings.ambientIntensity;
			originalSkybox = RenderSettings.skybox;

			// Apply map lighting
			Color previewColor = mapToUse?.Light ?? Color.white;
			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.ambientLight = previewColor;
			RenderSettings.ambientIntensity = 1f;

			// Apply map skybox
			var skyMat = mapToUse?.SkyboxMaterial;
			if (skyMat != null)
				RenderSettings.skybox = skyMat;
		}

		private void OnEndRender(ScriptableRenderContext context, Camera cam)
		{
			if (cam != GetComponent<Camera>()) return;

			//	// Restore
			RenderSettings.ambientProbe = originalProbe;
			RenderSettings.ambientLight = originalAmbientLight;
			RenderSettings.ambientMode = originalMode;
			RenderSettings.ambientIntensity = originalIntensity;
			RenderSettings.skybox = originalSkybox;   // ⭐ restore
		}

		public void SetMap(Map map) => mapToUse = map;
	}
}