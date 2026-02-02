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

			originalAmbientLight = RenderSettings.ambientLight;
			originalMode = RenderSettings.ambientMode;
			originalIntensity = RenderSettings.ambientIntensity;

			Color previewColor = mapToUse?.Light ?? Color.white;
			Debug.Log($"Preview ambient: {previewColor}");

			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.ambientLight = previewColor;
			RenderSettings.ambientIntensity = 1f;
		}


		//private void OnBeginRender(ScriptableRenderContext context, Camera cam)
		//{
		//	if (cam != GetComponent<Camera>()) return;

		//	// Save originals
		//	originalProbe = RenderSettings.ambientProbe;
		//	originalAmbientLight = RenderSettings.ambientLight;
		//	originalMode = RenderSettings.ambientMode;
		//	originalIntensity = RenderSettings.ambientIntensity;

		//	// Apply preview ambient
		//	Color previewColor = mapToUse?.Light ?? Color.white;
		//	Debug.Log($"Applying preview ambient for camera {cam.name}: {previewColor}");

		//	// Flat color → simple SH approximation (L2)
		//	// This gives roughly uniform ambient ≈ previewColor intensity
		//	var sh = new SphericalHarmonicsL2();

		//	// L0 (DC) term – roughly the average / diffuse ambient
		//	// Multiply by ~0.3183 (1/π) to match typical diffuse normalization
		//	Vector3 L0 = (Vector4)previewColor * 0.3183f;
		//	sh[0, 0] = L0.x; sh[0, 1] = L0.y; sh[0, 2] = L0.z;

		//	// Optional: add a tiny bit of directional tint if desired (e.g. fake sky/ground)
		//	// For pure flat → leave higher bands near zero
		//	// sh[1,0] = ... etc. for L1 (linear gradient)
		//	// sh[2,0..8] for L2 quadratic

		//	RenderSettings.ambientProbe = sh;
		//	RenderSettings.ambientLight = previewColor;
		//	RenderSettings.ambientMode = AmbientMode.Skybox; // or .Flat — Skybox makes URP treat it as probe
		//	RenderSettings.ambientIntensity = 1.0f;

		//	// Force URP to pick up the change (sometimes helps)
		//	// DynamicGI.UpdateEnvironment(); // usually not needed in per-camera hook
		//}

		private void OnEndRender(ScriptableRenderContext context, Camera cam)
		{
			if (cam != GetComponent<Camera>()) return;

			// Restore
			RenderSettings.ambientProbe = originalProbe;
			RenderSettings.ambientLight = originalAmbientLight;
			RenderSettings.ambientMode = originalMode;
			RenderSettings.ambientIntensity = originalIntensity;
		}

		// Optional: public setter if you want to change map later
		public void SetMap(Map map) => mapToUse = map;
	}
}