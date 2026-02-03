using UnityEngine;
using UnityEngine.Rendering;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	internal class CameraRenderSettingsOverride : MonoBehaviour
	{
		private UnityRenderSettings originalSettings;
		private UnityRenderSettings overrideSettings;   // The actual override values to apply

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

			// Save current global render settings
			originalSettings = UnityRenderSettings.Clone();

			// Apply the override values
			RenderSettings.ambientMode = overrideSettings.ambientMode;
			RenderSettings.ambientLight = overrideSettings.ambientLight;
			RenderSettings.ambientIntensity = overrideSettings.ambientIntensity;
			RenderSettings.skybox = overrideSettings.skybox;
			RenderSettings.ambientProbe = overrideSettings.ambientProbe;
			RenderSettings.subtractiveShadowColor = overrideSettings.subtractiveShadowColor;
		}

		private void OnEndRender(ScriptableRenderContext context, Camera cam)
		{
			if (cam != GetComponent<Camera>()) return;

			// Restore original settings
			UnityRenderSettings.Restore(originalSettings);
		}

		public void SetOverrideSettings(UnityRenderSettings value)
		{
			overrideSettings = value;
		}
	}
}