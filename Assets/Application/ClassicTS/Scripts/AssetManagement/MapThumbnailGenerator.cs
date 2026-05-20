using System;
using System;
using System.Collections;
using System.Linq;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	internal static class MapThumbnailGenerator
	{
		private const int DefaultWidth = 320;
		private const int DefaultHeight = 180;

		internal static int DefaultThumbnailWidth => DefaultWidth;
		internal static int DefaultThumbnailHeight => DefaultHeight;

		internal static IEnumerator CapturePng(Map map, Action<byte[]> onSuccess, Action<string> onError, int width = DefaultWidth, int height = DefaultHeight)
		{
			if (map == null)
			{
				onError?.Invoke("No map was provided for thumbnail capture.");
				yield break;
			}

			width = Mathf.Max(64, width);
			height = Mathf.Max(64, height);

			GameObject root = null;
			GameObject cameraObject = null;
			Texture2D captureTexture = null;
			RenderTexture renderTexture = null;
			Map previewMap = null;
			var originalSkybox = RenderSettings.skybox;
			var originalAmbientMode = RenderSettings.ambientMode;
			var originalAmbientLight = RenderSettings.ambientLight;
			var originalAmbientIntensity = RenderSettings.ambientIntensity;
			var originalShadowColor = RenderSettings.subtractiveShadowColor;

			try
			{
				root = new GameObject("MapThumbnailCapture")
				{
					hideFlags = HideFlags.HideAndDontSave
				};

				previewMap = MapUtils.Clone(map, root.transform);
				if (previewMap == null)
				{
					onError?.Invoke("Failed to clone the map for thumbnail capture.");
					yield break;
				}

				// Give the cloned map one frame to finish any component setup.
				yield return null;

				var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r != null && r.enabled).ToArray();
				if (!TryGetBounds(renderers, map, out var bounds))
				{
					onError?.Invoke("The map did not produce any visible geometry for a thumbnail.");
					yield break;
				}

				var renderSettings = map.RenderSettings;
				RenderSettings.skybox = renderSettings.skybox;
				RenderSettings.ambientMode = renderSettings.ambientMode;
				RenderSettings.ambientLight = renderSettings.ambientLight;
				RenderSettings.ambientIntensity = renderSettings.ambientIntensity;
				RenderSettings.subtractiveShadowColor = renderSettings.subtractiveShadowColor;

				cameraObject = new GameObject("MapThumbnailCamera")
				{
					hideFlags = HideFlags.HideAndDontSave
				};
				cameraObject.transform.SetParent(root.transform, false);

				var camera = cameraObject.AddComponent<Camera>();
				camera.enabled = false;
				camera.clearFlags = renderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
				camera.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
				camera.fieldOfView = 40f;
				camera.aspect = (float)width / height;
				camera.nearClipPlane = 0.03f;
				camera.farClipPlane = Mathf.Max(250f, bounds.size.magnitude * 10f);
				camera.cullingMask = ~0;

				// A soft oblique angle gives a readable list thumbnail without hiding too much of the map.
				var lookDirection = new Vector3(-1f, 0.85f, -1f).normalized;
				var distance = GetFramingDistance(bounds, camera.fieldOfView, width, height);
				var target = bounds.center + Vector3.up * Mathf.Max(0.25f, bounds.extents.y * 0.1f);
				var position = target - lookDirection * distance;
				camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));

				renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
				{
					name = "MapThumbnailRT",
					filterMode = FilterMode.Bilinear,
					antiAliasing = 1
				};
				renderTexture.Create();

				camera.targetTexture = renderTexture;
				camera.Render();

				captureTexture = TextureUtils.RenderTextureToTexture2D(renderTexture);
				if (captureTexture == null)
				{
					onError?.Invoke("Failed to read back the thumbnail render.");
					yield break;
				}

				byte[] png = captureTexture.EncodeToPNG();
				if (png == null || png.Length == 0)
				{
					onError?.Invoke("Thumbnail PNG encoding failed.");
					yield break;
				}

				onSuccess?.Invoke(png);
			}
			finally
			{
				RenderSettings.skybox = originalSkybox;
				RenderSettings.ambientMode = originalAmbientMode;
				RenderSettings.ambientLight = originalAmbientLight;
				RenderSettings.ambientIntensity = originalAmbientIntensity;
				RenderSettings.subtractiveShadowColor = originalShadowColor;

				if (captureTexture != null)
				{
					if (Application.isPlaying)
						UnityEngine.Object.Destroy(captureTexture);
					else
						UnityEngine.Object.DestroyImmediate(captureTexture);
				}

				if (renderTexture != null)
				{
					renderTexture.Release();
					if (Application.isPlaying)
						UnityEngine.Object.Destroy(renderTexture);
					else
						UnityEngine.Object.DestroyImmediate(renderTexture);
				}

				if (previewMap != null)
					previewMap.Destroy();

				if (cameraObject != null)
				{
					if (Application.isPlaying)
						UnityEngine.Object.Destroy(cameraObject);
					else
						UnityEngine.Object.DestroyImmediate(cameraObject);
				}

				if (root != null)
				{
					if (Application.isPlaying)
						UnityEngine.Object.Destroy(root);
					else
						UnityEngine.Object.DestroyImmediate(root);
				}
			}
		}

		internal static string BuildThumbnailKey(Map map)
		{
			if (map == null)
				return null;

			map.EnsureHashID();
			var key = HTB50Settings.ToString(map.HashID);
			if (!string.IsNullOrWhiteSpace(key))
				return key;

			return string.IsNullOrWhiteSpace(map.name)
				? null
				: map.name.Trim();
		}

		private static bool TryGetBounds(Renderer[] renderers, Map map, out Bounds bounds)
		{
			if (renderers != null && renderers.Length > 0)
			{
				bounds = renderers[0].bounds;
				for (int i = 1; i < renderers.Length; i++)
					bounds.Encapsulate(renderers[i].bounds);
				return true;
			}

			if (map != null && map.width > 0 && map.height > 0)
			{
				bounds = new Bounds(
					new Vector3(map.width * 0.5f, 0.5f, map.height * 0.5f),
					new Vector3(Mathf.Max(1f, map.width), Mathf.Max(1f, map.height), Mathf.Max(1f, Mathf.Max(map.width, map.height))));
				return true;
			}

			bounds = default;
			return false;
		}

		private static float GetFramingDistance(Bounds bounds, float fieldOfView, int width, int height)
		{
			float halfFovRad = Mathf.Max(0.01f, fieldOfView * 0.5f * Mathf.Deg2Rad);
			float aspect = Mathf.Max(0.25f, (float)width / Mathf.Max(1, height));
			float horizontal = Mathf.Max(bounds.extents.x, bounds.extents.z);
			float vertical = bounds.extents.y;

			float verticalDistance = vertical / Mathf.Tan(halfFovRad);
			float horizontalDistance = horizontal / Mathf.Tan(halfFovRad) / aspect;
			float radiusDistance = bounds.extents.magnitude / Mathf.Tan(halfFovRad);

			return Mathf.Max(6f, Mathf.Max(verticalDistance, Mathf.Max(horizontalDistance, radiusDistance)) * 1.12f);
		}
	}
}
