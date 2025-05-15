using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Camera))]
public class MirroredPassRenderer : MonoBehaviour
{
	public Vector3 planeNormal = Vector3.up;
	public float offset = 0f;
	[Range(float.Epsilon, 1f)]
	public float brightness = 1f;
	public Color skyboxBackgroundColor = new Color(0.1f, 0.1f, 0.2f); // Night sky fallback

	Camera mainCam;
	Camera mirrorCam;
	CommandBuffer mirrorCommandBuffer;
	CommandBuffer mainCamCommandBuffer;
	Light[] sceneLights;
	float[] originalLightIntensities;
	Color originalAmbientLight;
	float originalAmbientIntensity;
	CameraClearFlags originalCameraClearFlags;
	Material originalSkyboxMaterial;
	Material flippedSkyboxMaterial;

	void Start()
	{
		mainCam = Camera.main;
		originalCameraClearFlags = mainCam.clearFlags;
		Debug.Log("originalCameraClearFlags: " + originalCameraClearFlags);

		// Create mirror camera
		GameObject camObj = new GameObject("MirrorCamera");
		camObj.hideFlags = HideFlags.HideAndDontSave;
		mirrorCam = camObj.AddComponent<Camera>();
		mirrorCam.enabled = false;

		// Set up command buffer for mirror camera culling
		mirrorCommandBuffer = new CommandBuffer();
		mirrorCommandBuffer.name = "MirrorCameraCullingFix";
		mirrorCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, mirrorCommandBuffer);

		// Set up command buffer for main camera to reset culling
		mainCamCommandBuffer = new CommandBuffer();
		mainCamCommandBuffer.name = "MainCameraCullingReset";
		mainCamCommandBuffer.SetInvertCulling(false);
		mainCam.AddCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);

		// Create flipped skybox material
		if (RenderSettings.skybox != null)
		{
			Debug.Log("Skybox material found: " + RenderSettings.skybox.name);
			originalSkyboxMaterial = RenderSettings.skybox;
			flippedSkyboxMaterial = new Material(originalSkyboxMaterial); // Create instance
			flippedSkyboxMaterial.name = "FlippedNightskySkybox";

			// Log all texture properties
#if UNITY_EDITOR
			int propCount = ShaderUtil.GetPropertyCount(flippedSkyboxMaterial.shader);
			bool hasTextureProperties = false;
			for (int i = 0; i < propCount; i++)
			{
				if (ShaderUtil.GetPropertyType(flippedSkyboxMaterial.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
				{
					string propName = ShaderUtil.GetPropertyName(flippedSkyboxMaterial.shader, i);
					Texture tex = flippedSkyboxMaterial.GetTexture(propName);
					Debug.Log($"Texture property: {propName} = {(tex ? tex.name : "null")}");
					hasTextureProperties = true;
				}
			}
			if (!hasTextureProperties)
			{
				Debug.LogWarning("No texture properties found in shader");
			}
#else
            Debug.LogWarning("Shader property logging skipped in build (requires UnityEditor)");
#endif

			// Swap _UpTex and _DownTex to flip top/bottom
			string topTexProperty = "_UpTex";
			string bottomTexProperty = "_DownTex";
			if (flippedSkyboxMaterial.HasProperty(topTexProperty) && flippedSkyboxMaterial.HasProperty(bottomTexProperty))
			{
				Texture topTex = flippedSkyboxMaterial.GetTexture(topTexProperty);
				Texture bottomTex = flippedSkyboxMaterial.GetTexture(bottomTexProperty);


				if (topTex != null && bottomTex != null)
				{
					Debug.Log($"Swapping {topTexProperty} ({topTex.name}) and {bottomTexProperty} ({bottomTex.name})");
					flippedSkyboxMaterial.SetTexture(topTexProperty, bottomTex);
					flippedSkyboxMaterial.SetTexture(bottomTexProperty, topTex);
					// Ensure correct orientation
					flippedSkyboxMaterial.SetTextureOffset(topTexProperty, new Vector2(0, 0));
					flippedSkyboxMaterial.SetTextureScale(topTexProperty, new Vector2(1, 1));
					flippedSkyboxMaterial.SetTextureOffset(bottomTexProperty, new Vector2(0, 0));
					flippedSkyboxMaterial.SetTextureScale(bottomTexProperty, new Vector2(1, 1));
				}
				else
				{
					Debug.LogWarning($"Textures not found: {topTexProperty} = {(topTex ? topTex.name : "null")}, {bottomTexProperty} = {(bottomTex ? bottomTex.name : "null")}");
				}
			}
			else
			{
				Debug.LogWarning($"Skybox shader lacks properties: {topTexProperty} = {flippedSkyboxMaterial.HasProperty(topTexProperty)}, {bottomTexProperty} = {flippedSkyboxMaterial.HasProperty(bottomTexProperty)}");
			}

			// Flip side textures vertically (Front, Back, Left, Right)
			string[] sideTexProperties = new[] { "_UpTex", "_DownTex", "_FrontTex", "_BackTex", "_LeftTex", "_RightTex" };
			foreach (string sideProp in sideTexProperties)
			{
				if (flippedSkyboxMaterial.HasProperty(sideProp))
				{
					Texture sideTex = flippedSkyboxMaterial.GetTexture(sideProp);
					if (sideTex != null)
					{
						Debug.Log($"Flipping {sideProp} ({sideTex.name}) vertically");
						//flippedSkyboxMaterial.SetTextureScale(sideProp, new Vector2(1, -1));
						//flippedSkyboxMaterial.SetTextureOffset(sideProp, new Vector2(0, 1));

						var tx = flippedSkyboxMaterial.GetTexture(sideProp);
						tx.filterMode = FilterMode.Trilinear;
						tx.wrapMode = TextureWrapMode.Clamp;
						flippedSkyboxMaterial.SetTexture(sideProp, FlipTextureViaGPU(tx));
					}
					else
					{
						Debug.LogWarning($"Texture not found for {sideProp}");
					}
				}
				else
				{
					Debug.Log($"Side property {sideProp} not found in shader");
				}
			}

			RenderSettings.skybox = flippedSkyboxMaterial; // Use flipped material
		}
		else
		{
			Debug.LogWarning("No skybox material set in RenderSettings!");
		}

		// Ensure main camera uses Don't Clear for reflection
		mainCam.clearFlags = CameraClearFlags.Nothing;

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

		static Texture2D FlipTextureViaGPU(Texture originalTex)
		{
			int width = originalTex.width;
			int height = originalTex.height;

			RenderTexture rt = new RenderTexture(width, height, 0);
			RenderTexture.active = rt;
			Graphics.Blit(originalTex, rt);

			Texture2D flipped = new Texture2D(width, height, TextureFormat.RGBA32, false);
			flipped.ReadPixels(new Rect(0, 0, width, height), 0, 0);
			flipped.Apply();

			RenderTexture.active = null;
			rt.Release();

			return FlipTextureVertically(flipped);
		}

		static Texture2D FlipTextureVertically(Texture2D original)
		{
			int width = original.width;
			int height = original.height;
			Texture2D flipped = new Texture2D(width, height, original.format, false);
			flipped.wrapMode = original.wrapMode;
			flipped.filterMode = original.filterMode;

			for (int y = 0; y < height; y++)
			{
				flipped.SetPixels(0, y, width, 1, original.GetPixels(0, height - 1 - y, width, 1));
			}

			flipped.Apply();
			return flipped;
		}
	}

	void OnPreRender()
	{
		if (!mainCam || !mirrorCam) return;

		mirrorCam.CopyFrom(mainCam);
		mirrorCam.clearFlags = originalCameraClearFlags;
		mirrorCam.depth = mainCam.depth - 1;

		Vector3 normalizedNormal = planeNormal.normalized;
		Vector3 pointOnPlane = normalizedNormal * offset;

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

		Matrix4x4 translateToOrigin = Matrix4x4.Translate(-pointOnPlane);
		Matrix4x4 translateBack = Matrix4x4.Translate(pointOnPlane);
		reflectionMat = translateBack * reflectionMat * translateToOrigin;

		mirrorCam.worldToCameraMatrix = mainCam.worldToCameraMatrix * reflectionMat;

		mirrorCam.rect = new Rect(0, 0, 1, 1);

		for (int i = 0; i < sceneLights.Length; i++)
		{
			if (sceneLights[i].enabled)
			{
				sceneLights[i].intensity = originalLightIntensities[i] * brightness;
			}
		}

		RenderSettings.ambientLight = originalAmbientLight * brightness;
		RenderSettings.ambientIntensity = originalAmbientIntensity * brightness;

		mirrorCommandBuffer.Clear();
		mirrorCommandBuffer.SetInvertCulling(true);

		mirrorCam.Render();

		for (int i = 0; i < sceneLights.Length; i++)
		{
			if (sceneLights[i].enabled)
			{
				sceneLights[i].intensity = originalLightIntensities[i];
			}
		}
		RenderSettings.ambientLight = originalAmbientLight;
		RenderSettings.ambientIntensity = originalAmbientIntensity;

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
		if (mainCam != null)
		{
			if (mainCamCommandBuffer != null)
			{
				mainCam.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, mainCamCommandBuffer);
				mainCamCommandBuffer.Release();
			}
		}
		// Restore original skybox material
		if (originalSkyboxMaterial != null)
		{
			RenderSettings.skybox = originalSkyboxMaterial;
		}
		if (flippedSkyboxMaterial != null)
		{
			Destroy(flippedSkyboxMaterial);
		}
	}
}