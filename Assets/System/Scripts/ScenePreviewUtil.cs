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
			reflectionEffect?.OnRenderSettingsChanged(renderSettings);
		}

		public static void UpdateEffect(ReflectionEffectCamera.EffectMode effectMode = ReflectionEffectCamera.EffectMode.Null)
		{
			if (previewCam == null) return;

			var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect == null) return;
			reflectionEffect.SetEffectMode(effectMode, useDefaults: true);
			reflectionEffect.SetOffset(-0.2f);// default offset
			SetPreviewUI(targetRawImage);
		}

		public static void Update()
		{
			if (!targetRawImage || !previewCam) return;

			var reflectionEffect = previewCam.GetComponent<ReflectionEffectCamera>();
			if (!reflectionEffect) return;

			var rectTransform = targetRawImage.rectTransform;
			var currentSize = rectTransform.rect.size;

			if (currentSize.x <= 0 || currentSize.y <= 0)
			{
				targetRawImage.color = Color.red;//indicates error
				targetRawImage.enabled = false;
				reflectionEffect.gameObject.SetActive(false);
				return;
			}
			if (currentSize == lastKnownSize) return;
			lastKnownSize = currentSize;

			targetRawImage.color = Color.white;
			targetRawImage.enabled = true;
			reflectionEffect.gameObject.SetActive(true);

			var w = Mathf.RoundToInt(currentSize.x);
			var h = Mathf.RoundToInt(currentSize.y);

			reflectionEffect.SetExternalOutputTexture(null);

			previewCam.aspect = (float)w / h;
			var outputTexture = UpdateRenderTexture(w, h);

			reflectionEffect.SetExternalOutputTexture(outputTexture);

			targetRawImage.texture = outputTexture;
		}

		public static void SetPreviewUI(RawImage rawImage = null)
		{
			if (!rawImage) return;
			targetRawImage = rawImage;
			Update();
		}

		public static void Cleanup()
		{
			if (root)
				Object.DestroyImmediate(root);
			root = null;

			if (outputRenderTexture)
				Object.DestroyImmediate(outputRenderTexture);
			outputRenderTexture = null;

			previewCam = null;
			targetRawImage = null;
			lastKnownSize = Vector2.zero;
		}

		private static RenderTexture outputRenderTexture;
		private static RenderTexture UpdateRenderTexture(int targetWidth, int targetHeight)
		{
			var outputNeedsResize = outputRenderTexture == null || outputRenderTexture.width != targetWidth || outputRenderTexture.height != targetHeight;
			if (outputNeedsResize)
			{
				if (outputRenderTexture != null)
				{
					Object.DestroyImmediate(outputRenderTexture);
					outputRenderTexture = null;
				}

				outputRenderTexture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
				{
					name = "preview render texture",//  "$"Output_{effectMode}",
					useMipMap = false,
					autoGenerateMips = false,
					filterMode = FilterMode.Bilinear,
					useDynamicScale = true
				};
				outputRenderTexture.Create();
			}
			return outputRenderTexture;
		}
	}
}