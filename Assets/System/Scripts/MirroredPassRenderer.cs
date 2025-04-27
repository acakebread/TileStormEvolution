using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class MirroredPassRenderer : MonoBehaviour
{
	public Vector3 planeNormal = Vector3.up; // Normal of the reflection plane
	public float offset = 0f; // Distance from origin along the normal
	[Range(float.Epsilon, 1f)]
	public float brightness = 1f; // Brightness factor (0 = black, 1 = full, 0.5 = 50% blend with black)

	Camera mainCam;
	Camera mirrorCam;
	CommandBuffer mirrorCommandBuffer;
	CommandBuffer mainCamCommandBuffer;
	Light[] sceneLights;
	float[] originalLightIntensities;
	Color originalAmbientLight;
	float originalAmbientIntensity;

	void Start()
	{
		mainCam = Camera.main;

		// Create mirror camera
		GameObject camObj = new GameObject("MirrorCamera");
		camObj.hideFlags = HideFlags.HideAndDontSave;
		mirrorCam = camObj.AddComponent<Camera>();
		mirrorCam.enabled = false; // Disable automatic rendering for explicit control

		// Set up command buffer for mirror camera culling
		mirrorCommandBuffer = new CommandBuffer();
		mirrorCommandBuffer.name = "MirrorCameraCullingFix";
		mirrorCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, mirrorCommandBuffer);

		// Set up command buffer for main camera to reset culling
		mainCamCommandBuffer = new CommandBuffer();
		mainCamCommandBuffer.name = "MainCameraCullingReset";
		mainCamCommandBuffer.SetInvertCulling(false); // Ensure default culling
		mainCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);

		// Find all real-time lights in the scene
		sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
		originalLightIntensities = new float[sceneLights.Length];
		for (int i = 0; i < sceneLights.Length; i++)
		{
			originalLightIntensities[i] = sceneLights[i].intensity;
		}

		// Store original ambient light settings
		originalAmbientLight = RenderSettings.ambientLight;
		originalAmbientIntensity = RenderSettings.ambientIntensity;
	}

	void OnPreRender()
	{
		if (!mainCam || !mirrorCam) return;

		// Copy camera settings
		mirrorCam.CopyFrom(mainCam);
		mirrorCam.clearFlags = CameraClearFlags.Color;
		mirrorCam.depth = mainCam.depth - 1;

		// Normalize the plane normal
		Vector3 normalizedNormal = planeNormal.normalized;

		// Create reflection matrix for an arbitrary plane
		// Point on plane: p = offset * normalizedNormal
		Vector3 pointOnPlane = normalizedNormal * offset;

		// Householder reflection matrix: R = I - 2nn^T
		Matrix4x4 reflectionMat = Matrix4x4.identity;
		reflectionMat[0, 0] = 1 - 2 * normalizedNormal.x * normalizedNormal.x;
		reflectionMat[0, 1] = -2 * normalizedNormal.x * normalizedNormal.y;
		reflectionMat[0, 2] = -2 * normalizedNormal.x * normalizedNormal.z;
		reflectionMat[1, 0] = -2 * normalizedNormal.y * normalizedNormal.x;
		reflectionMat[1, 1] = 1 - 2 * normalizedNormal.y * normalizedNormal.y;
		reflectionMat[1, 2] = -2 * normalizedNormal.y * normalizedNormal.z;
		reflectionMat[2, 0] = -2 * normalizedNormal.z * normalizedNormal.x;
		reflectionMat[2, 1] = -2 * normalizedNormal.z * normalizedNormal.y;
		reflectionMat[2, 2] = 1 - 2 * normalizedNormal.z * normalizedNormal.z;

		// Translation to move plane to origin and back
		Matrix4x4 translateToOrigin = Matrix4x4.Translate(-pointOnPlane);
		Matrix4x4 translateBack = Matrix4x4.Translate(pointOnPlane);
		reflectionMat = translateBack * reflectionMat * translateToOrigin;

		// Get the main camera's world-to-camera matrix
		Matrix4x4 mainCamViewMatrix = mainCam.worldToCameraMatrix;

		// Apply reflection to the view matrix
		mirrorCam.worldToCameraMatrix = mainCamViewMatrix * reflectionMat;

		// Reset viewport to default
		mirrorCam.rect = new Rect(0, 0, 1, 1);

		// Scale light intensities for brightness
		for (int i = 0; i < sceneLights.Length; i++)
		{
			if (sceneLights[i].enabled)
			{
				sceneLights[i].intensity = originalLightIntensities[i] * brightness;
			}
		}

		// Scale ambient light
		RenderSettings.ambientLight = originalAmbientLight * brightness;
		RenderSettings.ambientIntensity = originalAmbientIntensity * brightness;

		// Update command buffer for mirror camera culling
		mirrorCommandBuffer.Clear();
		mirrorCommandBuffer.SetInvertCulling(true); // Invert culling for mirror camera

		// Explicitly render the mirror camera
		mirrorCam.Render();

		// Restore original light intensities and ambient light
		for (int i = 0; i < sceneLights.Length; i++)
		{
			if (sceneLights[i].enabled)
			{
				sceneLights[i].intensity = originalLightIntensities[i];
			}
		}
		RenderSettings.ambientLight = originalAmbientLight;
		RenderSettings.ambientIntensity = originalAmbientIntensity;

		// Reset culling state for subsequent renders
		mirrorCommandBuffer.SetInvertCulling(false);
	}

	void OnDestroy()
	{
		if (mirrorCam != null)
		{
			if (mirrorCommandBuffer != null)
			{
				mirrorCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mirrorCommandBuffer);
				mirrorCommandBuffer.Release();
			}
			Destroy(mirrorCam.gameObject);
		}
		if (mainCam != null && mainCamCommandBuffer != null)
		{
			mainCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);
			mainCamCommandBuffer.Release();
		}
	}
}
