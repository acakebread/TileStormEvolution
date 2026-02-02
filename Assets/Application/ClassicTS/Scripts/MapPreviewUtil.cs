#define USING_PREFAB

using MassiveHadronLtd;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ClassicTilestorm
{
	public static class MapPreviewUtil
	{
		private static GameObject root;
		private static GameObject camGO;
		private static Camera previewCam;
		private static RenderTexture renderTexture;

		public static Transform previewMapRoot;

		// ── Added for dynamic RT resize ─────────────────────────────────────
		private static RawImage targetRawImage;
		private static RectTransform previewRect;
		private static Vector2 lastKnownSize = Vector2.zero;

		public static Camera PreviewCamera => previewCam;
		public static RenderTexture PreviewRenderTexture => renderTexture;
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

		public static void Initialize(GameObject previewCamerPrefab, Map _map = null)
		{
			if (root != null) return;

			root = new GameObject("MAP_PREVIEW_ROOT");

			if (previewMapRoot != null) return;

			GameObject previewRoot = new GameObject("PreviewSceneRoot");
			previewRoot.transform.SetParent(root.transform);
			previewMapRoot = previewRoot.transform;

			renderTexture = new RenderTexture(512, 320, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
			renderTexture.Create();

			if (targetRawImage != null)
			{
				targetRawImage.texture = PreviewRenderTexture;
				targetRawImage.color = Color.white;
			}

#if USING_PREFAB
			camGO = GameObject.Instantiate(previewCamerPrefab);
			camGO.transform.SetParent(root.transform);
			camGO.layer = PreviewRenderLayers.previewLayer;

			previewCam = camGO.GetComponent<Camera>();
			previewCam.targetTexture = renderTexture;

			var reflectionEffect = camGO.GetComponent<ReflectionEffectCamera>();
			reflectionEffect.SetOffset(-0.2f);
#else
			camGO = new GameObject("PreviewCamera");
			camGO.transform.SetParent(root.transform);
			camGO.layer = PreviewRenderLayers.previewLayer;

			previewCam = camGO.AddComponent<Camera>();
			previewCam.cullingMask = PreviewRenderLayers.previewFullMask;
			previewCam.clearFlags = CameraClearFlags.SolidColor;
			previewCam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
			previewCam.nearClipPlane = 0.1f;
			previewCam.farClipPlane = 2000f;

			var reflectionEffect = camGO.AddComponent<ReflectionEffectCamera>();
			reflectionEffect.SetEffectMode(ReflectionEffectCamera.EffectMode.Water);
			reflectionEffect.SetOffset(-0.2f);
#endif

			// Add the override component
			var ambientOverride = camGO.AddComponent<PreviewAmbientOverride>();
			ambientOverride.SetMap(_map);  // pass your map reference
			Debug.Log($"PreviewAmbientOverride attached with map Light = {_map?.Light}");

			foreach (var childCam in camGO.GetComponentsInChildren<Camera>(true))  // true = include inactive
			{
				var overrideComp = childCam.gameObject.GetComponent<PreviewAmbientOverride>();
				if (overrideComp == null)
				{
					overrideComp = childCam.gameObject.AddComponent<PreviewAmbientOverride>();
				}
				overrideComp.SetMap(_map);  // safe even if duplicate calls

				Debug.Log($"Attached PreviewAmbientOverride to camera: {childCam.name} with Light = {_map?.Light}");
			}


			var previewSkyMat = SkyboxUtility.GetSkyboxMaterialForName(_map?.skybox);
			if (previewSkyMat != null)
				reflectionEffect.SetSkyboxOverride(previewSkyMat);
			else
				Debug.LogWarning($"Preview skybox not found for '{_map?.skybox}' — falling back to global.");

			UpdateRenderTextureSizeIfNeeded();

			//var provider = camGO.AddComponent<CameraCommandProvider>();
			//provider.RegisterCommand(
			//	RenderPassEvent.BeforeRenderingOpaques,  // or BeforeRenderingSkybox if sky/reflections matter
			//	(cmd, cam) =>
			//	{
			//		Color previewLight = _map?.Light ?? Color.white;

			//		// Most reliable for flat color override
			//		cmd.SetGlobalColor("unity_AmbientSky", previewLight);
			//		cmd.SetGlobalColor("unity_AmbientEquator", previewLight * 0.7f);  // or same as sky, or Color.black
			//		cmd.SetGlobalColor("unity_AmbientGround", previewLight * 0.4f);  // darker floor bounce simulation

			//		// Optional legacy fallback (some old shaders still read this)
			//		cmd.SetGlobalColor("UNITY_LIGHTMODEL_AMBIENT", previewLight);

			//		// If using SH-based ambient (common with light probes), you can approximate flat color like this:
			//		// But usually not needed if you're forcing the above
			//		// cmd.SetGlobalVector("unity_SHAr", new Vector4(previewLight.r, previewLight.g, previewLight.b, 1f) * someFactor);
			//	});
		}

		public static void SetActiveMap(Map map)
		{
			if (camGO == null) return;

			// Update ambient override on ALL preview cameras
			foreach (var overrideComp in camGO.GetComponentsInChildren<PreviewAmbientOverride>(true))
			{
				overrideComp.SetMap(map);
			}

			// Update skybox override too
			var reflectionEffect = camGO.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect != null)
			{
				var skyMat = SkyboxUtility.GetSkyboxMaterialForName(map?.Skybox);
				reflectionEffect.SetSkyboxOverride(skyMat);
			}

			Debug.Log($"Preview map updated → Light: {map?.Light}, Skybox: {map?.Skybox}");
		}

		public static void SetSkyboxOverride(Material value)
		{
			var reflectionEffect = camGO?.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect != null)
				reflectionEffect.SetSkyboxOverride(value);
		}

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
			previewMapRoot = null;

			// Added cleanup for resize fields
			targetRawImage = null;
			previewRect = null;
		}

		// ── Added helpers to set UI references from DatabaseEditorPanel ─────
		public static void SetPreviewUI(RawImage rawImage, RectTransform rectTransform)
		{
			targetRawImage = rawImage;
			previewRect = rectTransform;

			if (rawImage != null && renderTexture != null)
				rawImage.texture = renderTexture;
		}

  //      private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
  //      {
  //          private System.Collections.Generic.Dictionary<RenderPassEvent, System.Action<RasterCommandBuffer, Camera>> commands = new();

  //          public void RegisterCommand(RenderPassEvent evt, System.Action<RasterCommandBuffer, Camera> action)
  //          {
  //              commands[evt] = action;
  //          }

  //          public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

  //          public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
  //          {
  //              if (commands.TryGetValue(evt, out var action) && action != null)
  //              {
  //                  try { action.Invoke(commandBuffer, camera); }
  //                  catch (System.Exception e) { Debug.LogError($"Preview command error: {e}"); }
  //              }
  //          }

  //          private void OnDestroy() => commands.Clear();
		//}
	}

	public static class MapPreviewExtensions
	{
		public static GameObject InstantiatePreviewCopy(this Map map, Transform parent, int layer)
		{
			return map?.BuildPreviewGeometry(parent, layer);
		}
	}
}

