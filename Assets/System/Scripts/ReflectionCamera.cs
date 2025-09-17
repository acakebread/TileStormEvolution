using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ReflectionCamera : CommandBufferSettings
{
	[SerializeField] private Camera referenceCamera; // Overlay camera (Scene Camera)
	[SerializeField] public Vector3 planeNormal = Vector3.up; // Public for potential sharing
	[SerializeField] public float offset = -0.2f; // Public for potential sharing

	private Camera reflectionCamera;

	void Awake()
	{
		reflectionCamera = GetComponent<Camera>();
		reflectionCamera.clearFlags = CameraClearFlags.Depth;
		reflectionCamera.targetTexture = null; // Render to framebuffer

		if (referenceCamera == null)
		{
			Debug.LogError("Reference camera is null!", this);
			enabled = false;
			return;
		}

		reflectionCamera.cullingMask = referenceCamera.cullingMask;

		var commandBufferSettings = GetComponent<CommandBufferSettings>();
		if (commandBufferSettings == null)
		{
			Debug.LogError("CommandBufferSettings component missing on Reflection Camera");
			enabled = false;
			return;
		}

		commandBufferSettings.RegisterCommand(CommandBufferSettings.RenderPassMode.BeforeRenderingOpaques,
			(commandBuffer, camera) => commandBuffer.SetInvertCulling(true), reflectionCamera.name);
		commandBufferSettings.RegisterCommand(CommandBufferSettings.RenderPassMode.AfterRendering,
			(commandBuffer, camera) => commandBuffer.SetInvertCulling(false), reflectionCamera.name);
	}

	void LateUpdate()
	{
		if (referenceCamera == null || reflectionCamera == null) return;

		reflectionCamera.fieldOfView = referenceCamera.fieldOfView;
		reflectionCamera.nearClipPlane = referenceCamera.nearClipPlane;
		reflectionCamera.farClipPlane = referenceCamera.farClipPlane;
		reflectionCamera.aspect = referenceCamera.aspect;

		var n = planeNormal.normalized;
		var planePoint = n * offset;
		var reflectionMat = Matrix4x4.identity;
		reflectionMat[0, 0] = 1 - 2 * n.x * n.x;
		reflectionMat[0, 1] = -2 * n.x * n.y;
		reflectionMat[0, 2] = -2 * n.x * n.z;
		reflectionMat[1, 0] = -2 * n.y * n.x;
		reflectionMat[1, 1] = 1 - 2 * n.y * n.y;
		reflectionMat[1, 2] = -2 * n.y * n.z;
		reflectionMat[2, 0] = -2 * n.z * n.x;
		reflectionMat[2, 1] = -2 * n.z * n.y;
		reflectionMat[2, 2] = 1 - 2 * n.z * n.z;
		var translateToOrigin = Matrix4x4.Translate(-planePoint);
		var translateBack = Matrix4x4.Translate(planePoint);
		reflectionMat = translateBack * reflectionMat * translateToOrigin;

		reflectionCamera.worldToCameraMatrix = referenceCamera.worldToCameraMatrix * reflectionMat;
		reflectionCamera.projectionMatrix = referenceCamera.projectionMatrix;
	}
}