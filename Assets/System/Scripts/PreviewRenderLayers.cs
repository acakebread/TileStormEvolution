using UnityEngine;

namespace MassiveHadronLtd
{
	public static class PreviewRenderLayers
	{
		// ===== LAYER NAMES =====
		public const string LAYER_PREVIEW = "Preview";
		public const string LAYER_PREVIEW_TRANSPARENT = "PreviewTransparent";
		public const string LAYER_PREVIEW_EMISSIVE = "PreviewEmissive";

		// ===== CACHED LAYER INDICES =====
		public static readonly int previewLayer = LayerMask.NameToLayer(LAYER_PREVIEW);
		public static readonly int previewTransparentLayer = LayerMask.NameToLayer(LAYER_PREVIEW_TRANSPARENT);
		public static readonly int previewEmissiveLayer = LayerMask.NameToLayer(LAYER_PREVIEW_EMISSIVE);

		// ===== BITMASKS =====
		public static readonly int previewMask =
			(1 << previewLayer);

		public static readonly int previewFullMask =
			(1 << previewLayer) |
			(1 << previewTransparentLayer) |
			(1 << previewEmissiveLayer);

		static PreviewRenderLayers()
		{
			ValidateLayer(previewLayer, LAYER_PREVIEW);
			ValidateLayer(previewTransparentLayer, LAYER_PREVIEW_TRANSPARENT);
			ValidateLayer(previewEmissiveLayer, LAYER_PREVIEW_EMISSIVE);
		}

		static void ValidateLayer(int index, string name)
		{
			if (index < 0)
				Debug.LogError($"Layer '{name}' is not defined in Tags & Layers.");
		}

		// =========================================================
		// CAMERA
		// =========================================================

		public static void AddPreviewLayers(Camera cam, bool withTransparent = true)
		{
			if (!cam) return;
			cam.cullingMask |= withTransparent ? previewFullMask : previewMask;
		}

		public static void RemovePreviewLayers(Camera cam, bool withTransparent = true)
		{
			if (!cam) return;
			cam.cullingMask &= ~(withTransparent ? previewFullMask : previewMask);
		}

		// =========================================================
		// LIGHTS
		// =========================================================

		public static void SetPreviewLayersToLight(Light light, bool withTransparent = true)
		{
			if (!light) return;
			light.cullingMask = withTransparent ? previewFullMask : previewMask;
		}

		public static void AddPreviewLayersToLight(Light light, bool withTransparent = true)
		{
			if (!light) return;
			light.cullingMask |= withTransparent ? previewFullMask : previewMask;
		}

		public static void RemovePreviewLayersFromLight(Light light, bool withTransparent = true)
		{
			if (!light) return;
			light.cullingMask &= ~(withTransparent ? previewFullMask : previewMask);
		}

		// Bulk helpers

		public static void SetPreviewLayersToChildLights(Transform root, bool withTransparent = true)
		{
			var lights = root.GetComponentsInChildren<Light>(true);
			foreach (var l in lights)
				SetPreviewLayersToLight(l, withTransparent);
		}

		public static void AddPreviewLayersToChildLights(Transform root, bool withTransparent = true)
		{
			var lights = root.GetComponentsInChildren<Light>(true);
			foreach (var l in lights)
				AddPreviewLayersToLight(l, withTransparent);
		}

		public static void RemovePreviewLayersFromChildLights(Transform root, bool withTransparent = true)
		{
			var lights = root.GetComponentsInChildren<Light>(true);
			foreach (var l in lights)
				RemovePreviewLayersFromLight(l, withTransparent);
		}

		// =========================================================
		// GAMEOBJECT LAYER SETTING (optional but useful)
		// =========================================================

		public static void SetLayerRecursively(GameObject go, string layerName)
		{
			int layer = LayerMask.NameToLayer(layerName);
			if (layer < 0)
			{
				Debug.LogError($"Layer '{layerName}' not defined.");
				return;
			}

			SetLayerRecursively(go.transform, layer);
		}

		static void SetLayerRecursively(Transform t, int layer)
		{
			t.gameObject.layer = layer;
			foreach (Transform child in t)
				SetLayerRecursively(child, layer);
		}
	}
}