using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class NormalCullingCamera : MonoBehaviour
{
	private Camera targetCamera;
	private CommandBuffer cullingCommandBuffer;

	void Awake()
	{
		targetCamera = GetComponent<Camera>();

		// Log camera type
		var cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
		Debug.Log($"Normal Culling Camera {targetCamera.name} is {cameraData?.renderType}");

		// Validate Overlay
		if (cameraData?.renderType != CameraRenderType.Overlay)
		{
			Debug.LogWarning($"Normal Culling Camera {targetCamera.name} should be Overlay, not {cameraData?.renderType}!");
		}

		// Create CommandBuffer for normal culling
		cullingCommandBuffer = new CommandBuffer { name = "NormalCulling" };
		cullingCommandBuffer.SetInvertCulling(false);
		cullingCommandBuffer.SetGlobalFloat("_TestCommandBuffer", 0.0f);

		targetCamera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, cullingCommandBuffer);
		Debug.Log($"Normal Culling CommandBuffer added to {targetCamera.name}");
	}

	//void OnRenderObject() // Kept as you confirmed OnPreRender doesn't execute
	//{
	//	// Force normal culling execution
	//	Graphics.ExecuteCommandBuffer(cullingCommandBuffer);
	//	Debug.Log($"Normal Culling CommandBuffer executed for {targetCamera.name} at {Time.time}");
	//}

	void OnRenderObject() { cullingCommandBuffer.Clear(); cullingCommandBuffer.SetInvertCulling(false); cullingCommandBuffer.SetGlobalFloat("_TestCommandBuffer", 0.0f); Graphics.ExecuteCommandBuffer(cullingCommandBuffer); Debug.Log($"Forced normal culling for {targetCamera.name}"); } // NormalCullingCamera

	void OnDisable()
	{
		if (cullingCommandBuffer != null)
		{
			targetCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, cullingCommandBuffer);
			cullingCommandBuffer.Release();
		}
		Debug.Log($"Normal Culling CommandBuffer removed from {targetCamera.name}");
	}
}