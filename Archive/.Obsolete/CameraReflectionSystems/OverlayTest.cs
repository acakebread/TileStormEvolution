using UnityEngine;
using UnityEngine.Rendering.Universal;

public class OverlayTest : MonoBehaviour
{
	void Start()
	{
		var obj = new GameObject("Overlay Camera");
		obj.transform.SetParent(transform, false);

		var cam = obj.AddComponent<Camera>();
		cam.clearFlags = CameraClearFlags.Nothing; // ignored by URP
		cam.enabled = true;

		var data = obj.AddComponent<UniversalAdditionalCameraData>();
		data.renderType = CameraRenderType.Overlay;

		// Set clearDepth in a safe, “Inspector-like” way
		URPCameraHelper.SetClearDepth(data, false);

		var mainCam = GetComponent<Camera>();
		if (!mainCam.TryGetComponent<UniversalAdditionalCameraData>(out var mainData))
			mainData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();

		mainData.cameraStack.Add(cam);

		// Add to the main camera stack
		var mainCamera = GetComponent<Camera>();
		if (!mainCamera.TryGetComponent<UniversalAdditionalCameraData>(out var mainCameraData))
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

		mainCameraData.cameraStack.Add(cam);
	}
}
