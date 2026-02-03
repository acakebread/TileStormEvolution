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
		private static Camera previewCam;

		// Simple accessor — this is the transform where the panel should parent preview content
		public static Transform PreviewMapRoot => root != null ? root.transform : null;

		public static Camera PreviewCamera => previewCam;

		// ── Dynamic RT resize fields ────────────────────────────────────────
		private static RawImage targetRawImage;
		private static RectTransform previewRect;
		private static Vector2 lastKnownSize = Vector2.zero;

		public static void Initialize(Map map = null, GameObject previewCameraPrefab = null)
		{
			if (root != null) return;

			root = new GameObject("MAP_PREVIEW_ROOT");

			// Camera setup
			if (previewCameraPrefab != null)
			{
				var camInstance = GameObject.Instantiate(previewCameraPrefab);
				camInstance.transform.SetParent(root.transform);
				camInstance.layer = PreviewRenderLayers.previewLayer;
				previewCam = camInstance.GetComponent<Camera>();
			}
			else
			{
				var camGO = new GameObject("PreviewCamera");
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

			// Reflection & render settings
			var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect != null)
			{
				reflectionEffect.SetRenderToTextureMode(true);
				reflectionEffect.SetEffectMode(ReflectionEffectCamera.EffectMode.Water);
				reflectionEffect.SetOffset(-0.2f);
			}

			UpdateOverrideSettings(CreateRenderSettingsFromMap(map));

			//var renderSettings = CreateRenderSettingsFromMap(map);

			//// Ambient / render overrides on main camera + children
			//foreach (var childCam in previewCam.GetComponentsInChildren<Camera>(true))
			//{
			//	var overrideComp = childCam.gameObject.GetComponent<CameraRenderSettingsOverride>();
			//	if (overrideComp == null)
			//		overrideComp = childCam.gameObject.AddComponent<CameraRenderSettingsOverride>();
			//	overrideComp.SetOverrideSettings(renderSettings);
			//}

			// Skybox
			var previewSkyMat = SkyboxUtility.GetSkyboxMaterialForName(map?.skybox);
			if (previewSkyMat != null)
				reflectionEffect?.SetSkyboxOverride(previewSkyMat);
			else if (!string.IsNullOrEmpty(map?.skybox))
				Debug.LogWarning($"Preview skybox not found for '{map.skybox}' — falling back to global.");
		}

		public static void UpdateOverrideSettings(UnityRenderSettings renderSettings)
		{
			// Ambient / render overrides on main camera + children
			foreach (var childCam in previewCam.GetComponentsInChildren<Camera>(true))
			{
				var overrideComp = childCam.gameObject.GetComponent<CameraRenderSettingsOverride>();
				if (overrideComp == null)
					overrideComp = childCam.gameObject.AddComponent<CameraRenderSettingsOverride>();
				overrideComp.SetOverrideSettings(renderSettings);
			}
		}

		public static void SetActiveMap(Map map)
		{
			if (previewCam == null) return;

			// Update ambient overrides
			foreach (var overrideComp in previewCam.GetComponentsInChildren<CameraRenderSettingsOverride>(true))
				overrideComp.SetOverrideSettings(CreateRenderSettingsFromMap(map));

			// Skybox override
			var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
			reflectionEffect?.SetSkyboxOverride(map?.SkyboxMaterial);
		}

		public static void SetSkyboxOverride(Material value)
		{
			var reflectionEffect = previewCam?.GetComponent<ReflectionEffectCamera>();
			reflectionEffect?.SetSkyboxOverride(value);
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

			var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
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

			UpdateRenderTextureSizeIfNeeded();
		}

		public static void Cleanup()
		{
			if (root != null)
			{
				Object.DestroyImmediate(root);
			}

			root = null;
			previewCam = null;
			targetRawImage = null;
			previewRect = null;
			lastKnownSize = Vector2.zero;
		}

		public static UnityRenderSettings CreateRenderSettingsFromMap(Map map)
		{
			return new UnityRenderSettings(
				ambientMode: AmbientMode.Flat,
				ambientLight: map?.Light ?? Color.white,
				ambientIntensity: 1f,
				skybox: map?.SkyboxMaterial,
				ambientProbe: default,
				subtractiveShadowColor: RenderSettings.subtractiveShadowColor
			);
		}
	}
}