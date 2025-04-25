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

		// Reflect position and rotation across Y=0 plane
		Vector4 plane = new Vector4(0, 1, 0, yOffset);
		Matrix4x4 reflectionMat = MatrixReflect(plane);

		// Reflect position
		Vector4 pos = new Vector4(mainCam.transform.position.x, mainCam.transform.position.y, mainCam.transform.position.z, 1);
		pos = reflectionMat * pos;
		mirrorCam.transform.position = new Vector3(pos.x, pos.y, pos.z);

		// Reflect rotation
		Matrix4x4 mainCamWorldMatrix = mainCam.transform.localToWorldMatrix;
		Matrix4x4 reflectedWorldMatrix = reflectionMat * mainCamWorldMatrix;
		mirrorCam.transform.rotation = Quaternion.LookRotation(reflectedWorldMatrix.GetColumn(2), reflectedWorldMatrix.GetColumn(1));

		// Reset viewport to default
		mirrorCam.rect = new Rect(0, 0, 1, 1);

		// Apply horizontal flip via view matrix
		Matrix4x4 viewMatrix = mirrorCam.worldToCameraMatrix;
		Matrix4x4 flipMatrix = Matrix4x4.Scale(new Vector3(-1, 1, 1)); // Flip X-axis
		mirrorCam.worldToCameraMatrix = flipMatrix * viewMatrix;

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

	Matrix4x4 MatrixReflect(Vector4 plane)
	{
		Matrix4x4 reflectionMat = Matrix4x4.identity;

		float a = plane.x;
		float b = plane.y;
		float c = plane.z;
		float d = plane.w;

		float length = Mathf.Sqrt(a * a + b * b + c * c);
		if (length > 0)
		{
			a /= length;
			b /= length;
			c /= length;
			d /= length;
		}

		reflectionMat[0, 0] = -2 * a * a + 1;
		reflectionMat[0, 1] = -2 * a * b;
		reflectionMat[0, 2] = -2 * a * c;
		reflectionMat[0, 3] = -2 * a * d;

		reflectionMat[1, 0] = -2 * b * a;
		reflectionMat[1, 1] = -2 * b * b + 1;
		reflectionMat[1, 2] = -2 * b * c;
		reflectionMat[1, 3] = -2 * b * d;

		reflectionMat[2, 0] = -2 * c * a;
		reflectionMat[2, 1] = -2 * c * b;
		reflectionMat[2, 2] = -2 * c * c + 1;
		reflectionMat[2, 3] = -2 * c * d;

		reflectionMat[3, 0] = 0;
		reflectionMat[3, 1] = 0;
		reflectionMat[3, 2] = 0;
		reflectionMat[3, 3] = 1;

		return reflectionMat;
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


//using UnityEngine;
//using UnityEngine.Rendering;

//[RequireComponent(typeof(Camera))]
//public class MirroredPassRenderer : MonoBehaviour
//{
//	public float yOffset = 0f;
//	[Range(0f, 1f)]
//	public float brightness = 1f; // Brightness factor (0 = black, 1 = full, 0.5 = 50% blend with black)

//	Camera mainCam;
//	Camera mirrorCam;
//	CommandBuffer mirrorCommandBuffer;
//	CommandBuffer mainCamCommandBuffer;
//	Light[] sceneLights;
//	float[] originalLightIntensities;

//	void Start()
//	{
//		mainCam = Camera.main;

//		// Create mirror camera
//		GameObject camObj = new GameObject("MirrorCamera");
//		mirrorCam = camObj.AddComponent<Camera>();
//		mirrorCam.enabled = false; // Disable automatic rendering for explicit control

//		// Set up command buffer for mirror camera culling
//		mirrorCommandBuffer = new CommandBuffer();
//		mirrorCommandBuffer.name = "MirrorCameraCullingFix";
//		mirrorCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, mirrorCommandBuffer);

//		// Set up command buffer for main camera to reset culling
//		mainCamCommandBuffer = new CommandBuffer();
//		mainCamCommandBuffer.name = "MainCameraCullingReset";
//		mainCamCommandBuffer.SetInvertCulling(false); // Ensure default culling
//		mainCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);

//		// Find all real-time lights in the scene
//		sceneLights = FindObjectsOfType<Light>();
//		originalLightIntensities = new float[sceneLights.Length];
//		for (int i = 0; i < sceneLights.Length; i++)
//		{
//			originalLightIntensities[i] = sceneLights[i].intensity;
//		}
//	}

//	void OnPreRender()
//	{
//		if (!mainCam || !mirrorCam) return;

//		// Copy camera settings
//		mirrorCam.CopyFrom(mainCam);
//		mirrorCam.clearFlags = CameraClearFlags.Color;
//		mirrorCam.depth = mainCam.depth - 1;

//		// Reflect position and rotation across Y=0 plane
//		Vector4 plane = new Vector4(0, 1, 0, yOffset);
//		Matrix4x4 reflectionMat = MatrixReflect(plane);

//		// Reflect position
//		Vector4 pos = new Vector4(mainCam.transform.position.x, mainCam.transform.position.y, mainCam.transform.position.z, 1);
//		pos = reflectionMat * pos;
//		mirrorCam.transform.position = new Vector3(pos.x, pos.y, pos.z);

//		// Reflect rotation
//		Matrix4x4 mainCamWorldMatrix = mainCam.transform.localToWorldMatrix;
//		Matrix4x4 reflectedWorldMatrix = reflectionMat * mainCamWorldMatrix;
//		mirrorCam.transform.rotation = Quaternion.LookRotation(reflectedWorldMatrix.GetColumn(2), reflectedWorldMatrix.GetColumn(1));

//		// Reset viewport to default
//		mirrorCam.rect = new Rect(0, 0, 1, 1);

//		// Apply horizontal flip via view matrix
//		Matrix4x4 viewMatrix = mirrorCam.worldToCameraMatrix;
//		Matrix4x4 flipMatrix = Matrix4x4.Scale(new Vector3(-1, 1, 1)); // Flip X-axis
//		mirrorCam.worldToCameraMatrix = flipMatrix * viewMatrix;

//		// Scale light intensities for brightness
//		for (int i = 0; i < sceneLights.Length; i++)
//		{
//			//if (sceneLights[i].enabled && sceneLights[i].type != LightType.Directional) // Exclude directional lights if needed
//			if (sceneLights[i].enabled) // Exclude directional lights if needed
//			{
//				sceneLights[i].intensity = originalLightIntensities[i] * brightness;
//			}
//		}

//		// Update command buffer for mirror camera culling
//		mirrorCommandBuffer.Clear();
//		mirrorCommandBuffer.SetInvertCulling(true); // Invert culling for mirror camera

//		// Explicitly render the mirror camera
//		mirrorCam.Render();

//		// Restore original light intensities
//		for (int i = 0; i < sceneLights.Length; i++)
//		{
//			if (sceneLights[i].enabled)
//			{
//				sceneLights[i].intensity = originalLightIntensities[i];
//			}
//		}

//		// Reset culling state for subsequent renders
//		mirrorCommandBuffer.SetInvertCulling(false);
//	}

//	Matrix4x4 MatrixReflect(Vector4 plane)
//	{
//		Matrix4x4 reflectionMat = Matrix4x4.identity;

//		float a = plane.x;
//		float b = plane.y;
//		float c = plane.z;
//		float d = plane.w;

//		float length = Mathf.Sqrt(a * a + b * b + c * c);
//		if (length > 0)
//		{
//			a /= length;
//			b /= length;
//			c /= length;
//			d /= length;
//		}

//		reflectionMat[0, 0] = -2 * a * a + 1;
//		reflectionMat[0, 1] = -2 * a * b;
//		reflectionMat[0, 2] = -2 * a * c;
//		reflectionMat[0, 3] = -2 * a * d;

//		reflectionMat[1, 0] = -2 * b * a;
//		reflectionMat[1, 1] = -2 * b * b + 1;
//		reflectionMat[1, 2] = -2 * b * c;
//		reflectionMat[1, 3] = -2 * b * d;

//		reflectionMat[2, 0] = -2 * c * a;
//		reflectionMat[2, 1] = -2 * c * b;
//		reflectionMat[2, 2] = -2 * c * c + 1;
//		reflectionMat[2, 3] = -2 * c * d;

//		reflectionMat[3, 0] = 0;
//		reflectionMat[3, 1] = 0;
//		reflectionMat[3, 2] = 0;
//		reflectionMat[3, 3] = 1;

//		return reflectionMat;
//	}

//	void OnDestroy()
//	{
//		if (mirrorCam != null)
//		{
//			// Remove mirror camera command buffer
//			if (mirrorCommandBuffer != null)
//			{
//				mirrorCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mirrorCommandBuffer);
//				mirrorCommandBuffer.Release();
//			}
//			Destroy(mirrorCam.gameObject);
//		}
//		if (mainCam != null && mainCamCommandBuffer != null)
//		{
//			// Remove main camera command buffer
//			mainCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);
//			mainCamCommandBuffer.Release();
//		}
//	}
//}

////using UnityEngine;
////using UnityEngine.Rendering;

////[RequireComponent(typeof(Camera))]
////public class MirroredPassRenderer : MonoBehaviour
////{
////	public float yOffset = 0f;

////	Camera mainCam;
////	Camera mirrorCam;
////	CommandBuffer mirrorCommandBuffer;
////	CommandBuffer mainCamCommandBuffer;

////	void Start()
////	{
////		mainCam = Camera.main;

////		// Create mirror camera
////		GameObject camObj = new GameObject("MirrorCamera");
////		mirrorCam = camObj.AddComponent<Camera>();
////		mirrorCam.enabled = false; // Disable automatic rendering for explicit control

////		// Set up command buffer for mirror camera culling
////		mirrorCommandBuffer = new CommandBuffer();
////		mirrorCommandBuffer.name = "MirrorCameraCullingFix";
////		mirrorCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, mirrorCommandBuffer);

////		// Set up command buffer for main camera to reset culling
////		mainCamCommandBuffer = new CommandBuffer();
////		mainCamCommandBuffer.name = "MainCameraCullingReset";
////		mainCamCommandBuffer.SetInvertCulling(false); // Ensure default culling
////		mainCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);
////	}

////	void OnPreRender()
////	{
////		if (!mainCam || !mirrorCam) return;

////		// Copy camera settings
////		mirrorCam.CopyFrom(mainCam);
////		mirrorCam.clearFlags = CameraClearFlags.Color;
////		mirrorCam.depth = mainCam.depth - 1;

////		// Reflect position and rotation across Y=0 plane
////		Vector4 plane = new Vector4(0, 1, 0, yOffset);
////		Matrix4x4 reflectionMat = MatrixReflect(plane);

////		// Reflect position
////		Vector4 pos = new Vector4(mainCam.transform.position.x, mainCam.transform.position.y, mainCam.transform.position.z, 1);
////		pos = reflectionMat * pos;
////		mirrorCam.transform.position = new Vector3(pos.x, pos.y, pos.z);

////		// Reflect rotation
////		Matrix4x4 mainCamWorldMatrix = mainCam.transform.localToWorldMatrix;
////		Matrix4x4 reflectedWorldMatrix = reflectionMat * mainCamWorldMatrix;
////		mirrorCam.transform.rotation = Quaternion.LookRotation(reflectedWorldMatrix.GetColumn(2), reflectedWorldMatrix.GetColumn(1));

////		// Reset viewport to default
////		mirrorCam.rect = new Rect(0, 0, 1, 1);

////		// Apply horizontal flip via view matrix
////		Matrix4x4 viewMatrix = mirrorCam.worldToCameraMatrix;
////		Matrix4x4 flipMatrix = Matrix4x4.Scale(new Vector3(-1, 1, 1)); // Flip X-axis
////		mirrorCam.worldToCameraMatrix = flipMatrix * viewMatrix;

////		// Update command buffer for mirror camera culling and shadows
////		mirrorCommandBuffer.Clear();
////		mirrorCommandBuffer.SetInvertCulling(true); // Invert culling for mirror camera

////		// Explicitly render the mirror camera
////		mirrorCam.Render();

////		// Reset culling state for subsequent renders
////		mirrorCommandBuffer.SetInvertCulling(false);
////	}

////	Matrix4x4 MatrixReflect(Vector4 plane)
////	{
////		Matrix4x4 reflectionMat = Matrix4x4.identity;

////		float a = plane.x;
////		float b = plane.y;
////		float c = plane.z;
////		float d = plane.w;

////		float length = Mathf.Sqrt(a * a + b * b + c * c);
////		if (length > 0)
////		{
////			a /= length;
////			b /= length;
////			c /= length;
////			d /= length;
////		}

////		reflectionMat[0, 0] = -2 * a * a + 1;
////		reflectionMat[0, 1] = -2 * a * b;
////		reflectionMat[0, 2] = -2 * a * c;
////		reflectionMat[0, 3] = -2 * a * d;

////		reflectionMat[1, 0] = -2 * b * a;
////		reflectionMat[1, 1] = -2 * b * b + 1;
////		reflectionMat[1, 2] = -2 * b * c;
////		reflectionMat[1, 3] = -2 * b * d;

////		reflectionMat[2, 0] = -2 * c * a;
////		reflectionMat[2, 1] = -2 * c * b;
////		reflectionMat[2, 2] = -2 * c * c + 1;
////		reflectionMat[2, 3] = -2 * c * d;

////		reflectionMat[3, 0] = 0;
////		reflectionMat[3, 1] = 0;
////		reflectionMat[3, 2] = 0;
////		reflectionMat[3, 3] = 1;

////		return reflectionMat;
////	}

////	void OnDestroy()
////	{
////		if (mirrorCam != null)
////		{
////			// Remove mirror camera command buffer
////			if (mirrorCommandBuffer != null)
////			{
////				mirrorCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mirrorCommandBuffer);
////				mirrorCommandBuffer.Release();
////			}
////			Destroy(mirrorCam.gameObject);
////		}
////		if (mainCam != null && mainCamCommandBuffer != null)
////		{
////			// Remove main camera command buffer
////			mainCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);
////			mainCamCommandBuffer.Release();
////		}
////	}
////}