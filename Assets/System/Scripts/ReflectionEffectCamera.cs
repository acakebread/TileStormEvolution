

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
		URPWater
	}

	[SerializeField] private EffectMode effectMode = EffectMode.PerfectMirror;

	// Used for frost effect
	[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
	[SerializeField] private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

	// Used for surface film effect
	[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
	[SerializeField, Range(0.1f, 50f)] private float noiseScale = 1f;
	[SerializeField] private Texture2D noiseTexture;

	// Used for URPWater effect
	[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
	[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
	[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
	[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;

	private Camera mainCamera;
	private Camera reflectionCamera;
	private Camera textureCamera;
	private RenderTexture renderTexture;
	private Mesh effectMesh;
	private Material effectMaterial;
	private bool isMaterialDynamic;
	private bool isTextureDynamic;
	private float timeSeed; // For URPWater time-based animation

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
		provider.RegisterCommand(RenderPassEvent.AfterRenderingTransparents, (cmd, cam) => { cmd.SetInvertCulling(false); });

		var data = obj.AddComponent<UniversalAdditionalCameraData>();
		data.renderType = CameraRenderType.Overlay;
		URPCameraHelper.SetClearDepth(data, false);

		// Initialize effect settings
		InitializeEffect();
	}

	private void InitializeEffect()
	{
		// Clean up existing dynamic resources
		CleanupDynamicResources();

		// Initialize noise texture for SurfaceFilm or FrostEffect
		if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect)
		{
			noiseTexture = noiseTexture != null ? noiseTexture : TextureUtils.GeneratePerlinNoiseTexture();
			isTextureDynamic = noiseTexture == null;
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
				effectMaterial = MaterialUtils.CreateFrostMaterial(baseColor, frostDepth, renderTexture, noiseTexture, 0.02f);
				isMaterialDynamic = true;

				SetupTextureCamera();
				reflectionCamera.targetTexture = renderTexture;
				outputStage = mainCamera;
				break;

			case EffectMode.URPWater:
				SetupRenderTexture("WaterRenderTexture");
				effectMesh = new Mesh();
				effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
				isMaterialDynamic = true;

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

		// Update material properties
		UpdateMaterialProperties();

		// Register rendering command
		if (outputStage != null)
		{
			var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
			if (provider == null)
				provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

			provider.RegisterCommand(RenderPassEvent.AfterRendering,
				(cmd, cam) =>
				{
					if (null == effectMesh) return;
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
		if (renderTexture != null && effectMode != EffectMode.FrostEffect && effectMode != EffectMode.URPWater)
		{
			DestroyImmediate(renderTexture);
			renderTexture = null;
		}
		if (isTextureDynamic && noiseTexture != null)
		{
			DestroyImmediate(noiseTexture);
			noiseTexture = null;
		}
		if (textureCamera != null && effectMode != EffectMode.FrostEffect && effectMode != EffectMode.URPWater)
		{
			DestroyImmediate(textureCamera.gameObject);
			textureCamera = null;
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

	private void UpdateMaterialProperties()
	{
		if (effectMaterial != null)
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
					effectMaterial.SetTexture("_NoiseTex", noiseTexture);
					break;
				case EffectMode.URPWater:
					effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
					effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
					effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
					effectMaterial.SetFloat("_RippleOffset", rippleOffset);
					effectMaterial.SetFloat("_TimeSeed", timeSeed);
					break;
			}
		}
	}

	public void Update()
	{
		UpdateMaterialProperties();
		if (effectMode == EffectMode.URPWater)
		{
			timeSeed += Time.deltaTime;
		}
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

		InitializeEffect();
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