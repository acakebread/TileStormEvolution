using UnityEngine;
using UnityEngine.Rendering;

public class SkyboxCameraClear : MonoBehaviour
{
	private Camera skyboxCam;
	private CommandBuffer clearCommandBuffer;

	void Start()
	{
		skyboxCam = GetComponent<Camera>();
		if (skyboxCam == null)
		{
			Debug.LogError("SkyboxCameraClear script requires a Camera component!");
			return;
		}

		// Create command buffer for clearing color only
		clearCommandBuffer = new CommandBuffer();
		clearCommandBuffer.name = "ClearColorOnly";

		// Clear color buffer only (preserve depth)
		clearCommandBuffer.ClearRenderTarget(false, true, skyboxCam.backgroundColor);

		// Add command buffer before skybox rendering
		skyboxCam.AddCommandBuffer(CameraEvent.BeforeSkybox, clearCommandBuffer);
	}

	void OnDestroy()
	{
		if (skyboxCam != null && clearCommandBuffer != null)
		{
			skyboxCam.RemoveCommandBuffer(CameraEvent.BeforeSkybox, clearCommandBuffer);
			clearCommandBuffer.Release();
		}
	}
}