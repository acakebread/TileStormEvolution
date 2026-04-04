using UnityEngine;
using UnityEngine.Rendering;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	internal class CameraRenderSettingsOverride : MonoBehaviour
	{
		private UnityRenderSettings originalSettings;
		private UnityRenderSettings overrideSettings;   // The actual override values to apply
		public UnityRenderSettings OverrideSettings { get => overrideSettings; set => overrideSettings = value; }

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
			originalSettings = UnityRenderSettings.CaptureCurrent();

			// Apply the override values
			RenderSettings.skybox = overrideSettings.skybox;

			RenderSettings.ambientMode = overrideSettings.ambientMode;
			RenderSettings.ambientLight = overrideSettings.ambientLight;
			RenderSettings.ambientIntensity = overrideSettings.ambientIntensity;
			RenderSettings.ambientProbe = overrideSettings.ambientProbe;

			RenderSettings.subtractiveShadowColor = overrideSettings.subtractiveShadowColor;
		}

		private void OnEndRender(ScriptableRenderContext context, Camera cam)
		{
			if (cam != GetComponent<Camera>()) return;

			// Restore original settings
			UnityRenderSettings.Restore(originalSettings);
		}
	}
}