using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class ReflectionEffectCamera : MonoBehaviour
{
	private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
	{
		private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

		public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
		{
			if (commands.ContainsKey(evt) && commands[evt] != null)
			{
				try { commands[evt].Invoke(commandBuffer, camera); }
				catch (Exception e) { Debug.LogError($"CameraCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}"); }
			}
		}

		void OnDestroy() => commands.Clear();
	}

	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset = 0f;

	public enum EffectMode
	{
		Debug,
		PerfectMirror,
		SurfaceFilm,
		FrostEffect,
		Water,
		OceanEffect
	}

	[SerializeField] private EffectMode effectMode = EffectMode.PerfectMirror;
	[SerializeField, HideInInspector] private EffectMode previousEffectMode;

	// Used for frost effect
	[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
	[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;
	[SerializeField] private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

	// Used for surface film effect
	[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
	[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
	[SerializeField] private Texture2D noiseTexture;

	// Used for water effect
	[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
	[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
	[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
	[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
	[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

	// Used for ocean effect
	[SerializeField, Range(0f, 1f)] private float frostThreshold = 0.8f;
	[SerializeField, Range(0f, 0.2f)] private float frostFadeRange = 0.1f;

	private Camera mainCamera;
	private Camera reflectionCamera;
	private Camera textureCamera;
	[SerializeField] private Camera postProcessingCamera;
	private RenderTexture renderTexture;
	private Mesh effectMesh;
	private Material effectMaterial;
	private bool isMaterialDynamic;
	private bool isTextureDynamic;
	private float timeSeed;

	// Track previous values for change detection
	private Color lastBaseColor;
	private float lastFrostDepth;
	private float lastNoiseStrength;
	private float lastFilmIntensity;
	private float lastNoiseScale;
	private Texture2D lastNoiseTexture;
	private float lastRippleSpeed;
	private float lastRippleAmplitude;
	private float lastRippleFrequency;
	private float lastRippleOffset;
	private float lastReflectionStrength;
	private float lastFrostThreshold;
	private float lastFrostFadeRange;
	private Material lastSkyboxMaterial;

	void Start()
	{
		mainCamera = GetComponent<Camera>();
		if (mainCamera == null)
		{
			Debug.LogError("Camera component missing.", this);
			enabled = false;
			return;
		}

		// Initialize reflection camera
		var obj = new GameObject("ReflectionCamera");
		obj.transform.SetParent(transform, false);
		reflectionCamera = obj.AddComponent<Camera>();
		reflectionCamera.clearFlags = CameraClearFlags.Nothing;
		reflectionCamera.cullingMask = mainCamera.cullingMask;

		var provider = obj.AddComponent<CameraCommandProvider>();
		if (provider == null)
		{
			Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCamera", this);
			enabled = false;
			return;
		}

		provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
		provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => { cmd.SetInvertCulling(false); });

		var data = obj.AddComponent<UniversalAdditionalCameraData>();
		data.renderType = CameraRenderType.Overlay;
		URPCameraHelper.SetClearDepth(data, false);

		// Initialize effect and set previousEffectMode
		InitializeEffect();
		previousEffectMode = effectMode;
		// Initialize tracked values
		StoreMaterialPropertyValues();
	}

	private void InitializeEffect()
	{
		// Clean up existing dynamic resources
		CleanupDynamicResources();

		// Initialize noise texture for SurfaceFilm, FrostEffect, or OceanEffect
		if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
		{
			if (noiseTexture == null)
			{
				noiseTexture = TextureUtils.GeneratePerlinNoiseTexture();
				isTextureDynamic = true;
			}
			else
			{
				isTextureDynamic = false;
			}
		}
		else
		{
			// Clean up noise texture if not needed
			if (isTextureDynamic && noiseTexture != null)
			{
				DestroyImmediate(noiseTexture);
				noiseTexture = null;
				isTextureDynamic = false;
			}
		}

		Camera outputStage = null;
		switch (effectMode)
		{
			case EffectMode.PerfectMirror:
				effectMesh = new Mesh();
				effectMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
				isMaterialDynamic = true;

				var mirrorData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
				mirrorData.cameraStack.Clear();
				mirrorData.cameraStack.Add(reflectionCamera);

				reflectionCamera.targetTexture = null;
				outputStage = reflectionCamera;
				break;

			case EffectMode.SurfaceFilm:
				effectMesh = new Mesh();
				effectMaterial = MaterialUtils.CreateSurfaceFilmMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f), noiseTexture, filmIntensity, noiseScale);
				isMaterialDynamic = true;

				var filmData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
				filmData.cameraStack.Clear();
				filmData.cameraStack.Add(reflectionCamera);

				reflectionCamera.targetTexture = null;
				outputStage = reflectionCamera;
				break;

			case EffectMode.FrostEffect:
				SetupRenderTexture("RenderTexture");
				effectMesh = new Mesh();
				effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, renderTexture, noiseTexture, noiseStrength);
				isMaterialDynamic = true;

				SetupTextureCamera();
				reflectionCamera.targetTexture = renderTexture;
				outputStage = mainCamera;
				break;

			case EffectMode.Water:
				SetupRenderTexture("WaterRenderTexture");
				effectMesh = new Mesh();
				effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
				isMaterialDynamic = true;

				SetupTextureCamera();
				reflectionCamera.targetTexture = renderTexture;
				outputStage = mainCamera;
				break;

			case EffectMode.OceanEffect:
				SetupRenderTexture("OceanRenderTexture");
				effectMesh = new Mesh();
				effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, renderTexture, noiseTexture);
				isMaterialDynamic = true;

				if (renderTexture == null) Debug.LogError("OceanEffect: renderTexture is null!");
				if (noiseTexture == null) Debug.LogWarning("OceanEffect: noiseTexture is null, using generated Perlin noise.");
				Debug.Log($"OceanEffect: Material created with shader {effectMaterial.shader.name}");

				SetupTextureCamera();
				reflectionCamera.targetTexture = renderTexture;
				outputStage = mainCamera;
				break;

			default:
				var defaultData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
				defaultData.cameraStack.Clear();
				defaultData.cameraStack.Add(reflectionCamera);

				outputStage = mainCamera;
				break;
		}

		var mainCameraData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
		if (null != postProcessingCamera) mainCameraData.cameraStack.Add(postProcessingCamera);

		// Update material properties and skybox
		UpdateMaterialProperties();
		SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);

		// Register rendering command
		if (outputStage != null)
		{
			var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
			if (provider == null)
				provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

			provider.RegisterCommand(RenderPassEvent.AfterRendering,
				(cmd, cam) =>
				{
					if (effectMesh == null) return;
					FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);
					if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
					{
						effectMaterial.SetPass(0);
						cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
					}
				}
			);
		}
	}

	private void CleanupDynamicResources()
	{
		if (effectMaterial != null && isMaterialDynamic)
		{
			DestroyImmediate(effectMaterial);
			effectMaterial = null;
		}
		if (renderTexture != null && effectMode != EffectMode.FrostEffect && effectMode != EffectMode.Water && effectMode != EffectMode.OceanEffect)
		{
			DestroyImmediate(renderTexture);
			renderTexture = null;
		}
		if (isTextureDynamic && noiseTexture != null)
		{
			DestroyImmediate(noiseTexture);
			noiseTexture = null;
		}
		if (textureCamera != null && effectMode != EffectMode.FrostEffect && effectMode != EffectMode.Water && effectMode != EffectMode.OceanEffect)
		{
			DestroyImmediate(textureCamera.gameObject);
			textureCamera = null;
		}
		if (effectMesh != null)
		{
			DestroyImmediate(effectMesh);
			effectMesh = null;
		}
	}

	private void SetupRenderTexture(string textureName)
	{
		if (renderTexture == null)
		{
			renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
			{
				name = textureName,
				useMipMap = false,
				autoGenerateMips = false,
				filterMode = FilterMode.Bilinear,
				useDynamicScale = true
			};
			renderTexture.Create();
		}
	}

	private void SetupTextureCamera()
	{
		if (textureCamera == null)
		{
			var obj = new GameObject("TextureCamera");
			obj.transform.SetParent(transform, false);
			textureCamera = obj.AddComponent<Camera>();
			textureCamera.CopyFrom(mainCamera);
			textureCamera.clearFlags = mainCamera.clearFlags;
			textureCamera.cullingMask = mainCamera.cullingMask;
			textureCamera.targetTexture = renderTexture;
			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.cameraStack.Clear();
			data.cameraStack.Add(reflectionCamera);
		}
	}

	private bool HasMaterialPropertiesChanged()
	{
		return baseColor != lastBaseColor ||
			   frostDepth != lastFrostDepth ||
			   noiseStrength != lastNoiseStrength ||
			   filmIntensity != lastFilmIntensity ||
			   noiseScale != lastNoiseScale ||
			   noiseTexture != lastNoiseTexture ||
			   rippleSpeed != lastRippleSpeed ||
			   rippleAmplitude != lastRippleAmplitude ||
			   rippleFrequency != lastRippleFrequency ||
			   rippleOffset != lastRippleOffset ||
			   reflectionStrength != lastReflectionStrength ||
			   frostThreshold != lastFrostThreshold ||
			   frostFadeRange != lastFrostFadeRange ||
			   RenderSettings.skybox != lastSkyboxMaterial;
	}

	private void StoreMaterialPropertyValues()
	{
		lastBaseColor = baseColor;
		lastFrostDepth = frostDepth;
		lastNoiseStrength = noiseStrength;
		lastFilmIntensity = filmIntensity;
		lastNoiseScale = noiseScale;
		lastNoiseTexture = noiseTexture;
		lastRippleSpeed = rippleSpeed;
		lastRippleAmplitude = rippleAmplitude;
		lastRippleFrequency = rippleFrequency;
		lastRippleOffset = rippleOffset;
		lastReflectionStrength = reflectionStrength;
		lastFrostThreshold = frostThreshold;
		lastFrostFadeRange = frostFadeRange;
		lastSkyboxMaterial = RenderSettings.skybox;
	}

	private void UpdateMaterialProperties()
	{
		if (effectMaterial == null)
			return;

		// Only update static properties if they have changed
		if (HasMaterialPropertiesChanged())
		{
			effectMaterial.SetColor("_BaseColor", baseColor);
			switch (effectMode)
			{
				case EffectMode.SurfaceFilm:
					effectMaterial.SetFloat("_FilmIntensity", filmIntensity);
					effectMaterial.SetFloat("_NoiseScale", noiseScale);
					effectMaterial.SetTexture("_NoiseTex", noiseTexture);
					break;
				case EffectMode.FrostEffect:
					effectMaterial.SetFloat("_Depth", frostDepth);
					effectMaterial.SetFloat("_NoiseStrength", noiseStrength);
					effectMaterial.SetTexture("_NoiseTex", noiseTexture);
					break;
				case EffectMode.Water:
					effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
					effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
					effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
					effectMaterial.SetFloat("_RippleOffset", rippleOffset);
					effectMaterial.SetFloat("_ReflectionStrength", reflectionStrength);
					if (RenderSettings.skybox != lastSkyboxMaterial)
						SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);
					break;
				case EffectMode.OceanEffect:
					effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
					effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
					effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
					effectMaterial.SetFloat("_RippleOffset", rippleOffset);
					effectMaterial.SetFloat("_DepthThreshold", 128.0f); // Maps to _DepthMax, default 128
					effectMaterial.SetFloat("_FrostDepth", frostDepth); // Maps to _Depth
					effectMaterial.SetFloat("_FrostNoiseStrength", noiseStrength); // Maps to _NoiseStrength
					effectMaterial.SetFloat("_FrostThreshold", frostThreshold);
					effectMaterial.SetFloat("_FrostFadeRange", frostFadeRange);
					effectMaterial.SetTexture("_NoiseTex", noiseTexture);
					if (RenderSettings.skybox != lastSkyboxMaterial)
						SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);
					break;
			}

			// Store the updated values
			StoreMaterialPropertyValues();
		}

		// Always update timeSeed for Water and OceanEffect
		if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
		{
			effectMaterial.SetFloat("_TimeSeed", timeSeed);
			if (effectMode == EffectMode.OceanEffect)
				effectMaterial.SetFloat("_RippleSeed", timeSeed);
		}
	}

	public void Update()
	{
		// Update timeSeed every frame for Water and OceanEffect
		if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
		{
			timeSeed += Time.deltaTime;
		}

		// Update material properties (will skip static properties if unchanged)
		UpdateMaterialProperties();
	}

	private void LateUpdate()
	{
		if (reflectionCamera != null)
		{
			reflectionCamera.fieldOfView = mainCamera.fieldOfView;
			reflectionCamera.nearClipPlane = mainCamera.nearClipPlane;
			reflectionCamera.farClipPlane = mainCamera.farClipPlane;
			reflectionCamera.aspect = mainCamera.aspect;
			reflectionCamera.clearFlags = CameraClearFlags.Nothing;

			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
			reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
			reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
		}

		if (textureCamera != null)
		{
			textureCamera.fieldOfView = mainCamera.fieldOfView;
			textureCamera.nearClipPlane = mainCamera.nearClipPlane;
			textureCamera.farClipPlane = mainCamera.farClipPlane;
			textureCamera.aspect = mainCamera.aspect;
			textureCamera.orthographic = mainCamera.orthographic;
			textureCamera.orthographicSize = mainCamera.orthographicSize;
		}
	}

	public void OnValidate()
	{
		if (!isActiveAndEnabled || mainCamera == null)
			return;

		// Only reinitialize if effectMode has changed
		if (effectMode != previousEffectMode)
		{
			InitializeEffect();
			previousEffectMode = effectMode;
		}
		else
		{
			// Update material properties without recreating resources
			UpdateMaterialProperties();
		}
	}

	public void ForceSkyboxUpdate()
	{
		if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
		{
			SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);
			lastSkyboxMaterial = RenderSettings.skybox;
		}
	}

	void OnDestroy()
	{
		if (mainCamera != null)
			mainCamera.targetTexture = null;

		if (reflectionCamera != null)
			reflectionCamera.targetTexture = null;

		if (textureCamera != null)
			textureCamera.targetTexture = null;

		if (effectMaterial != null && isMaterialDynamic)
			DestroyImmediate(effectMaterial);

		if (effectMesh != null)
			DestroyImmediate(effectMesh);

		if (renderTexture != null)
			DestroyImmediate(renderTexture);

		if (isTextureDynamic && noiseTexture != null)
			DestroyImmediate(noiseTexture);
	}
}