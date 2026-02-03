using MassiveHadronLtd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClassicTilestorm
{
	public static class MapPreviewUtil
	{
		private static GameObject root;
		private static GameObject camGO;
		private static Camera previewCam;

		public static Transform previewMapRoot;

		// ── Added for dynamic RT resize ─────────────────────────────────────
		private static RawImage targetRawImage;
		private static RectTransform previewRect;
		private static Vector2 lastKnownSize = Vector2.zero;

		public static Camera PreviewCamera => previewCam;
		public static Transform PreviewMapRoot => previewMapRoot;

		public static void ClearPreviewMap()
		{
			if (previewMapRoot != null)
			{
				foreach (Transform child in previewMapRoot)
				{
					if (child != null) Object.DestroyImmediate(child.gameObject);
				}
			}
		}

		public static void Initialize(Map _map = null, GameObject previewCamerPrefab = null)
		{
			if (root != null) return;

			root = new GameObject("MAP_PREVIEW_ROOT");

			GameObject previewRoot = new GameObject("PreviewSceneRoot");
			previewRoot.transform.SetParent(root.transform);
			previewMapRoot = previewRoot.transform;

			if (null != previewCamerPrefab)
			{
				camGO = GameObject.Instantiate(previewCamerPrefab);
				camGO.transform.SetParent(root.transform);
				camGO.layer = PreviewRenderLayers.previewLayer;

				previewCam = camGO.GetComponent<Camera>();
			}
			else
			{
				camGO = new GameObject("PreviewCamera");
				camGO.transform.SetParent(root.transform);
				camGO.layer = PreviewRenderLayers.previewLayer;

				previewCam = camGO.AddComponent<Camera>();
				camGO.AddComponent<UniversalAdditionalCameraData>();
				previewCam.cullingMask = PreviewRenderLayers.previewFullMask;
				previewCam.clearFlags = CameraClearFlags.Skybox;
				previewCam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
				previewCam.nearClipPlane = 0.1f;
				previewCam.farClipPlane = 2000f;
				camGO.AddComponent<ReflectionEffectCamera>();
			}

			var reflectionEffect = camGO.GetComponent<ReflectionEffectCamera>();
			// ── Key change: enable internal RT management ─────────────────────────────
			reflectionEffect.SetRenderToTextureMode(true);
			reflectionEffect.SetEffectMode(ReflectionEffectCamera.EffectMode.Water);  // or whatever default you want
			reflectionEffect.SetOffset(-0.2f);

			// Add the override component
			var ambientOverride = camGO.AddComponent<PreviewRenderSettingsOverride>();
			ambientOverride.SetMap(_map);

			foreach (var childCam in camGO.GetComponentsInChildren<Camera>(true))
			{
				var overrideComp = childCam.gameObject.GetComponent<PreviewRenderSettingsOverride>();
				if (overrideComp == null)
					overrideComp = childCam.gameObject.AddComponent<PreviewRenderSettingsOverride>();
				overrideComp.SetMap(_map);
			}

			var previewSkyMat = SkyboxUtility.GetSkyboxMaterialForName(_map?.skybox);
			if (previewSkyMat != null)
				reflectionEffect.SetSkyboxOverride(previewSkyMat);
			else
				Debug.LogWarning($"Preview skybox not found for '{_map?.skybox}' — falling back to global.");

			// Initial size — will be updated properly in UpdateRenderTextureSizeIfNeeded
			UpdateRenderTextureSizeIfNeeded();
		}

		public static void SetActiveMap(Map map)
		{
			if (camGO == null) return;

			// Update ambient override on ALL preview cameras
			foreach (var overrideComp in camGO.GetComponentsInChildren<PreviewRenderSettingsOverride>(true))
				overrideComp.SetMap(map);

			// Update skybox override too
			var reflectionEffect = camGO.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect != null)
				reflectionEffect.SetSkyboxOverride(map?.SkyboxMaterial);
		}

		public static void SetSkyboxOverride(Material value)
		{
			var reflectionEffect = camGO?.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect != null)
				reflectionEffect.SetSkyboxOverride(value);
		}

		public static void UpdateRenderTextureSizeIfNeeded()
		{
			if (previewRect == null || previewCam == null) return;

			Vector2 currentSize = previewRect.rect.size;
			if (currentSize.x <= 0 || currentSize.y <= 0) return;
			if (currentSize == lastKnownSize) return;
			if (currentSize.x < 16 || currentSize.y < 16) return;

			lastKnownSize = currentSize;

			int w = Mathf.RoundToInt(currentSize.x);
			int h = Mathf.RoundToInt(currentSize.y);

			previewCam.aspect = (float)w / h;

			var reflectionEffect = previewCam?.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect != null)
			{
				reflectionEffect.CreateOrResizeReflectionTexture(w, h);

				if (targetRawImage != null)
				{
					targetRawImage.texture = reflectionEffect.GetOutputTexture();
					targetRawImage.color = Color.white;
				}
			}
		}

		public static void Cleanup()
		{
			if (root) Object.DestroyImmediate(root);

			root = null;
			camGO = null;
			previewCam = null;
			previewMapRoot = null;

			targetRawImage = null;
			previewRect = null;
		}

		public static void SetPreviewUI(RawImage rawImage, RectTransform rectTransform)
		{
			targetRawImage = rawImage;
			previewRect = rectTransform;

			if (rawImage != null && previewCam != null)
			{
				var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
				if (reflectionEffect != null)
				{
					rawImage.texture = reflectionEffect.GetOutputTexture();
					rawImage.color = Color.white;
				}
			}

			// Trigger resize to apply new rect size immediately
			UpdateRenderTextureSizeIfNeeded();
		}
	}

	public static class MapPreviewExtensions
	{
		public static GameObject InstantiatePreviewCopy(this Map map, Transform parent, int layer) => map?.BuildPreviewGeometry(parent, layer);
	}

	[RequireComponent(typeof(Camera))]
	internal class PreviewRenderSettingsOverride : MonoBehaviour
	{
		[SerializeField] private Map mapToUse;

		private SphericalHarmonicsL2 originalProbe;
		private Color originalAmbientLight;
		private AmbientMode originalMode;
		private float originalIntensity;
		private Material originalSkybox;

		//private RenderSettings overrideRenderSettings;//ToDo convert to using overrideRenderSettings instead of Map to make it general purpose

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
			RenderSettings.skybox = originalSkybox;
		}

		public void SetMap(Map map) => mapToUse = map;
	}
}

