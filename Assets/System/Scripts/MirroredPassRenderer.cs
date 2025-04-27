using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class MirroredPassRenderer : MonoBehaviour
{
	public float yOffset = 0f;
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

		// Find all real-time lights in the scene (non-deprecated)
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

		// Create reflection matrix for Y=yOffset plane
		Matrix4x4 scale = Matrix4x4.Scale(new Vector3(1, -1, 1)); // Flip Y-axis
		Matrix4x4 translate = Matrix4x4.Translate(new Vector3(0, 2 * yOffset, 0)); // Translate by 2 * yOffset
		Matrix4x4 reflectionMat = scale * translate; // Combine: scale first, then translate

		// Get the main camera's world-to-camera matrix
		Matrix4x4 mainCamViewMatrix = mainCam.worldToCameraMatrix;

		// Apply reflection to the view matrix
		mirrorCam.worldToCameraMatrix = mainCamViewMatrix * reflectionMat;

		// Reset viewport to default
		mirrorCam.rect = new Rect(0, 0, 1, 1);

		// Scale light intensities for brightness (include directional lights)
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
			// Remove mirror camera command buffer
			if (mirrorCommandBuffer != null)
			{
				mirrorCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mirrorCommandBuffer);
				mirrorCommandBuffer.Release();
			}
			Destroy(mirrorCam.gameObject);
		}
		if (mainCam != null && mainCamCommandBuffer != null)
		{
			// Remove main camera command buffer
			mainCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);
			mainCamCommandBuffer.Release();
		}
	}
}