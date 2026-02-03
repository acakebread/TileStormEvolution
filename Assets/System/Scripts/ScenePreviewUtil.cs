using UnityEngine;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	public static class ScenePreviewUtil
	{
		private static GameObject root;
		private static Camera previewCam;

		// Where content should be parented
		public static Transform PreviewMapRoot => root != null ? root.transform : null;

		public static Camera PreviewCamera => previewCam;

		// ── Dynamic RT resize fields ────────────────────────────────────────
		private static RawImage targetRawImage;
		private static Vector2 lastKnownSize = Vector2.zero;

		public static void Initialize(UnityRenderSettings renderSettings, GameObject previewCameraPrefab = null)
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
				camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

				previewCam.cullingMask = PreviewRenderLayers.previewFullMask;
				previewCam.clearFlags = CameraClearFlags.Skybox;
				previewCam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
				previewCam.nearClipPlane = 0.1f;
				previewCam.farClipPlane = 2000f;

				camGO.AddComponent<ReflectionEffectCamera>();
			}

			// Reflection & render settings
			var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
			if (null == reflectionEffect) return;

			reflectionEffect.SetEffectMode(ReflectionEffectCamera.EffectMode.Water);
			reflectionEffect.SetOffset(-0.2f);
			UpdateRenderSettings(renderSettings);
		}

		public static void UpdateRenderSettings(UnityRenderSettings renderSettings)
		{
			if (null == previewCam) return;
			var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
			reflectionEffect?.UpdateRenderSettings(renderSettings);
		}

		public static void Update()
		{
			if (targetRawImage == null || previewCam == null) return;

			var rectTransform = targetRawImage.rectTransform;
			Vector2 currentSize = rectTransform.rect.size;

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

				targetRawImage.texture = reflectionEffect.GetOutputTexture();
				targetRawImage.color = Color.white;
			}
		}

		public static void SetPreviewUI(RawImage rawImage = null)
		{
			var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
			if (null == reflectionEffect) return;
			reflectionEffect.SetRenderToTextureMode(null != rawImage);
			if (null == rawImage) return;

			targetRawImage = rawImage;

			if (rawImage != null && previewCam != null)
			{
				rawImage.texture = reflectionEffect.GetOutputTexture();
				rawImage.color = Color.white;
			}

			Update();
		}

		public static void Cleanup()
		{
			if (root != null)
				Object.DestroyImmediate(root);

			root = null;
			previewCam = null;
			targetRawImage = null;
			lastKnownSize = Vector2.zero;
		}
	}
}