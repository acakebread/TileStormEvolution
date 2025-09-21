using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class OverlayTest : MonoBehaviour
{
	void Start()
	{
		var obj = new GameObject("Overlay Camera");
		obj.transform.SetParent(transform, false);

		var camera = obj.AddComponent<Camera>();
		camera.clearFlags = CameraClearFlags.Nothing; // URP ignores this for overlay depth
		camera.enabled = true;
		camera.fieldOfView = 120;

		var data = obj.AddComponent<UniversalAdditionalCameraData>();
		data.renderType = CameraRenderType.Overlay;

		// --- Try a public setter if URP added one in future versions ---
		var prop = typeof(UniversalAdditionalCameraData).GetProperty(
			"clearDepth", BindingFlags.Instance | BindingFlags.Public);
		if (prop != null && prop.CanWrite)
		{
			prop.SetValue(data, false);
		}
		else
		{
			// --- Fallback: set the private serialized backing field used in most URP versions ---
			var field = typeof(UniversalAdditionalCameraData).GetField(
				"m_ClearDepth", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null)
				field.SetValue(data, false);
			else
				Debug.LogWarning("Couldn't set clearDepth on UniversalAdditionalCameraData (field not found). " +
					"Consider creating the overlay camera in the Editor or using a prefab with Clear Depth off.");
		}

		// Add to the main camera stack
		var mainCamera = GetComponent<Camera>();
		if (!mainCamera.TryGetComponent<UniversalAdditionalCameraData>(out var mainCameraData))
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

		mainCameraData.cameraStack.Add(camera);
	}
}
