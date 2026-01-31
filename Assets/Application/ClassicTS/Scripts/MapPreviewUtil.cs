using MassiveHadronLtd;
using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public static class MapPreviewUtil
	{
		private static GameObject root;
		private static GameObject camGO;
		private static Camera previewCam;
		private static RenderTexture renderTexture;

		private static GameObject previewRoot;
		public static Transform previewMapRoot;

		public static int previewLayer = -1;
		public const string PREVIEW_LAYER_NAME = "Preview";

		// ── Added for dynamic RT resize ─────────────────────────────────────
		private static RawImage targetRawImage;
		private static RectTransform previewRect;
		private static Vector2 lastKnownSize = Vector2.zero;

		public static Camera PreviewCamera => previewCam;
		public static RenderTexture PreviewRenderTexture => renderTexture;
		public static Transform PreviewMapRoot => previewMapRoot;

		public static void SetPreviewLayer(int layer)
		{
			previewLayer = layer;
		}

		private static void EnsurePreviewRoot()
		{
			if (previewRoot != null) return;

			previewRoot = new GameObject("PreviewSceneRoot");
			previewRoot.transform.SetParent(root.transform);
			previewMapRoot = new GameObject("MapCopy").transform;
			previewMapRoot.SetParent(previewRoot.transform);
			previewMapRoot.localPosition = Vector3.zero;
		}

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

		public static void Initialize(Map _map = null)
		{
			if (root != null) return;

			root = new GameObject("MAP_PREVIEW_ROOT");

			int previewLayerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
			if (previewLayerIndex < 0)
			{
				Debug.LogError($"Layer '{PREVIEW_LAYER_NAME}' not found in Tags and Layers!");
				previewLayerIndex = 0; // fallback
			}

			previewLayer = 1 << previewLayerIndex;

			// Root camera setup
			camGO = new GameObject("PreviewCamera");
			camGO.transform.SetParent(root.transform);

			previewCam = camGO.AddComponent<Camera>();
			previewCam.clearFlags = CameraClearFlags.SolidColor;
			previewCam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
			previewCam.cullingMask = 1 << previewLayerIndex;
			previewCam.nearClipPlane = 0.1f;
			previewCam.farClipPlane = 2000f;

			var reflectionEffect = camGO.AddComponent<ReflectionEffectCamera>();
			reflectionEffect.SetEffectMode(ReflectionEffectCamera.EffectMode.Water);
			reflectionEffect.SetOffset(-0.2f);

			var previewSkyMat = SkyboxUtility.GetSkyboxMaterialForName(_map?.skybox);
			if (previewSkyMat != null)
				reflectionEffect.SetSkyboxOverride(previewSkyMat);
			else
				Debug.LogWarning($"Preview skybox not found for '{_map?.skybox}' — falling back to global.");

			// Dedicated preview light
			var lightGO = new GameObject("PreviewDirectionalLight");
			lightGO.transform.SetParent(root.transform);
			lightGO.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);

			var previewLight = lightGO.AddComponent<Light>();
			previewLight.type = LightType.Directional;
			previewLight.intensity = 0.5f;
			previewLight.color = new Color(0.75f, 0.75f, 0.75f);
			previewLight.shadows = LightShadows.Soft;
			previewLight.shadowStrength = 0.75f;
			previewLight.renderMode = LightRenderMode.ForcePixel;
			previewLight.cullingMask = 1 << previewLayerIndex;

			// Render Texture (initial fixed size — will be resized dynamically)
			renderTexture = new RenderTexture(512, 320, 24, RenderTextureFormat.ARGB32)
			{
				filterMode = FilterMode.Bilinear
			};
			renderTexture.Create();
			previewCam.targetTexture = renderTexture;

			//CreateGroundPlane();
			EnsurePreviewRoot();
		}

		public static void SetSkyboxOverride(Material value)
		{
			var reflectionEffect = camGO?.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect != null)
				reflectionEffect.SetSkyboxOverride(value);
		}

		public static void Render()
		{
			if (previewCam != null && previewCam.isActiveAndEnabled)
				previewCam.Render();
		}

		// ── Added: dynamic resize based on RawImage rect size ────────────────
		public static void UpdateRenderTextureSizeIfNeeded()
		{
			if (previewRect == null || previewCam == null || renderTexture == null) return;

			Vector2 currentSize = previewRect.rect.size;
			if (currentSize.x <= 0 || currentSize.y <= 0)
				return; // don't resize to invalid size

			if (currentSize == lastKnownSize) return;
			if (currentSize.x < 16 || currentSize.y < 16) return;

			lastKnownSize = currentSize;

			int w = Mathf.RoundToInt(currentSize.x);
			int h = Mathf.RoundToInt(currentSize.y);

			if (renderTexture.width == w && renderTexture.height == h)
				return;

			// Release and recreate
			renderTexture.Release();
			renderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
			{
				filterMode = FilterMode.Bilinear
			};
			renderTexture.Create();

			previewCam.targetTexture = renderTexture;
			previewCam.aspect = (float)w / h;

			if (targetRawImage != null)
				targetRawImage.texture = renderTexture;
		}

		public static void Cleanup()
		{
			if (renderTexture != null && renderTexture.IsCreated())
			{
				renderTexture.Release();
				renderTexture = null;
			}

			if (root) Object.DestroyImmediate(root);

			root = null;
			camGO = null;
			previewCam = null;
			previewRoot = null;
			previewMapRoot = null;

			// Added cleanup for resize fields
			targetRawImage = null;
			previewRect = null;
			lastKnownSize = Vector2.zero;
		}

		// ── Added helpers to set UI references from DatabaseEditorPanel ─────
		public static void SetPreviewUI(RawImage rawImage, RectTransform rectTransform)
		{
			targetRawImage = rawImage;
			previewRect = rectTransform;

			if (rawImage != null && renderTexture != null)
				rawImage.texture = renderTexture;
		}
	}

	public static class MapPreviewExtensions
	{
		public static GameObject InstantiatePreviewCopy(this Map map, Transform parent, int layer)
		{
			return map?.BuildPreviewGeometry(parent, layer);
		}
	}
}