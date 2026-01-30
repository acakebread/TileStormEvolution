using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
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
		public void SetOffset(float value) => offset = value;

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
		public void SetEffectMode(EffectMode value)
		{
			effectMode = value;
			if (value == EffectMode.Water)
				ApplyWaterDefault();
		}

		// === MIRROR & FILM TINT (NEW) ===
		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

		// === FROST / WATER / OCEAN BASE COLOR (OLD) ===
		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

		// Used for surface film effect
		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
		[SerializeField] private Texture2D noiseTexture;

		// Used for frost effect
		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

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

		// Track previous values
		private Color lastMirrorTint;
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
			if (Camera.main == mainCamera) mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Preview"));//make sure to remove Preview from cullling if main camera

			var obj = new GameObject("ReflectionCamera");
			obj.transform.SetParent(transform, false);
			reflectionCamera = obj.AddComponent<Camera>();
			reflectionCamera.clearFlags = CameraClearFlags.Nothing;
			reflectionCamera.cullingMask = mainCamera.cullingMask;
			reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
			//reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("TransparentFX"));//not sure

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

			InitializeEffect();
			previousEffectMode = effectMode;
			StoreMaterialPropertyValues();

			//mainCamera.cullingMask |= 1 << LayerMask.NameToLayer("Editor");//no need for this any more
		}

		private void InitializeEffect()
		{
			CleanupDynamicResources();

			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
			{
				if (noiseTexture == null)
				{
					//noiseTexture = TextureUtils.GeneratePerlinNoiseTexture();
					//noiseTexture = TextureUtils.GenerateWangTileAtlas();
					noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
					isTextureDynamic = true;
				}
				else
				{
					isTextureDynamic = false;
				}
			}
			else
			{
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
					SetupRenderTexture("RenderTexture");
					effectMesh = new Mesh();
					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(renderTexture, mirrorTint);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					outputStage = mainCamera;
					break;

				case EffectMode.SurfaceFilm:
					SetupRenderTexture("MirrorWithFilmRT");
					effectMesh = new Mesh();
					//effectMaterial = MaterialUtils.CreateMirrorWithFilmOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					outputStage = mainCamera;
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

			UpdateMaterialProperties();
			SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);

			if (outputStage != null)
			{
				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
				if (provider == null)
					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
				(cmd, cam) =>
				{
					if (effectMesh == null) return;
					//FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);
					FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh, true);
					if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
					{
						effectMaterial.SetPass(0);
						cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
					}
				});
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
				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("TransparentFX"));
				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
				textureCamera.targetTexture = renderTexture;
				textureCamera.depth = mainCamera.depth - 1;
				var data = obj.AddComponent<UniversalAdditionalCameraData>();
				data.cameraStack.Clear();
				data.cameraStack.Add(reflectionCamera);
				obj.AddComponent<CameraCommandProvider>();
			}
		}

		private bool HasMaterialPropertiesChanged()
		{
			return mirrorTint != lastMirrorTint ||
				   baseColor != lastBaseColor ||
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
			lastMirrorTint = mirrorTint;
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
			if (effectMaterial == null) return;

			if (HasMaterialPropertiesChanged())
			{
				switch (effectMode)
				{
					case EffectMode.PerfectMirror:
					case EffectMode.SurfaceFilm:
						effectMaterial.SetColor("_DimColor", mirrorTint);
						if (effectMode == EffectMode.SurfaceFilm)
						{
							effectMaterial.SetFloat("_FilmIntensity", filmIntensity);
							effectMaterial.SetFloat("_NoiseScale", noiseScale);
							effectMaterial.SetTexture("_NoiseTex", noiseTexture);
						}
						break;

					case EffectMode.FrostEffect:
						effectMaterial.SetColor("_BaseColor", baseColor);
						effectMaterial.SetFloat("_Depth", frostDepth);
						effectMaterial.SetFloat("_NoiseStrength", noiseStrength);
						effectMaterial.SetTexture("_NoiseTex", noiseTexture);
						break;

					case EffectMode.Water:
						effectMaterial.SetColor("_BaseColor", baseColor);
						effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
						effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
						effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
						effectMaterial.SetFloat("_RippleOffset", rippleOffset);
						effectMaterial.SetFloat("_ReflectionStrength", reflectionStrength);
						if (RenderSettings.skybox != lastSkyboxMaterial)
							SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);
						break;

					case EffectMode.OceanEffect:
						effectMaterial.SetColor("_BaseColor", baseColor);
						effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
						effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
						effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
						effectMaterial.SetFloat("_RippleOffset", rippleOffset);
						effectMaterial.SetFloat("_DepthThreshold", 128.0f);
						effectMaterial.SetFloat("_FrostDepth", frostDepth);
						effectMaterial.SetFloat("_FrostNoiseStrength", noiseStrength);
						effectMaterial.SetFloat("_FrostThreshold", frostThreshold);
						effectMaterial.SetFloat("_FrostFadeRange", frostFadeRange);
						effectMaterial.SetTexture("_NoiseTex", noiseTexture);
						if (RenderSettings.skybox != lastSkyboxMaterial)
							SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);
						break;
				}

				StoreMaterialPropertyValues();
			}

			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
			{
				effectMaterial.SetFloat("_TimeSeed", timeSeed);
				if (effectMode == EffectMode.OceanEffect)
					effectMaterial.SetFloat("_RippleSeed", timeSeed);
			}
		}

		public void ApplyWaterDefault()
		{
			baseColor = new Color(0, 0, 0, 0.5f);
			rippleSpeed = 0.25f;
			rippleAmplitude = 0.25f;
			rippleFrequency = 0.35f;
			reflectionStrength = 0.5f;
		}

		public void Update()
		{
			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
			{
				timeSeed += Time.deltaTime;
			}

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

			if (effectMode != previousEffectMode)
			{
				InitializeEffect();
				previousEffectMode = effectMode;
			}
			else
			{
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

		private void CleanupDynamicResources()
		{
			if (effectMaterial != null && isMaterialDynamic)
			{
				DestroyImmediate(effectMaterial);
				effectMaterial = null;
			}
			if (effectMesh != null)
			{
				DestroyImmediate(effectMesh);
				effectMesh = null;
			}
			if (noiseTexture != null && isTextureDynamic)
			{
				DestroyImmediate(noiseTexture);
				noiseTexture = null;
			}
			if (renderTexture != null && effectMode != EffectMode.FrostEffect && effectMode != EffectMode.Water && effectMode != EffectMode.OceanEffect)
			{
				DestroyImmediate(renderTexture);
				renderTexture = null;
			}
			if (textureCamera != null && effectMode != EffectMode.FrostEffect && effectMode != EffectMode.Water && effectMode != EffectMode.OceanEffect)
			{
				DestroyImmediate(textureCamera.gameObject);
				textureCamera = null;
			}
		}

		void OnDestroy()
		{
			if (mainCamera != null) mainCamera.targetTexture = null;
			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
			if (textureCamera != null) textureCamera.targetTexture = null;
			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
			if (effectMesh != null) DestroyImmediate(effectMesh);
			if (renderTexture != null) DestroyImmediate(renderTexture);
			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);
		}
	}
}




//using System;
//using UnityEngine;
//using System.Collections.Generic;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//namespace MassiveHadronLtd
//{
//	[RequireComponent(typeof(Camera))]
//	public class ReflectionEffectCamera : MonoBehaviour
//	{
//		private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
//		{
//			private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

//			public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

//			public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

//			public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
//			{
//				if (commands.ContainsKey(evt) && commands[evt] != null)
//				{
//					try { commands[evt].Invoke(commandBuffer, camera); }
//					catch (Exception e) { Debug.LogError($"CameraCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}"); }
//				}
//			}

//			void OnDestroy() => commands.Clear();
//		}

//		[SerializeField, Tooltip("Optional override skybox material for this reflection setup (used in water/ocean reflection)")]
//		private Material overrideSkyboxMaterial;

//		[SerializeField] private Vector3 planeNormal = Vector3.up;
//		[SerializeField] private float offset = 0f;
//		public void SetOffset(float value) => offset = value;

//		public enum EffectMode
//		{
//			Debug,
//			PerfectMirror,
//			SurfaceFilm,
//			FrostEffect,
//			Water,
//			OceanEffect
//		}

//		[SerializeField] private EffectMode effectMode = EffectMode.PerfectMirror;
//		[SerializeField, HideInInspector] private EffectMode previousEffectMode;
//		public void SetEffectMode(EffectMode value) => effectMode = value;

//		// === MIRROR & FILM TINT (NEW) ===
//		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
//		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

//		// === FROST / WATER / OCEAN BASE COLOR (OLD) ===
//		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
//		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

//		// Used for surface film effect
//		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
//		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
//		[SerializeField] private Texture2D noiseTexture;

//		// Used for frost effect
//		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

//		// Used for water effect
//		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

//		// Used for ocean effect
//		[SerializeField, Range(0f, 1f)] private float frostThreshold = 0.8f;
//		[SerializeField, Range(0f, 0.2f)] private float frostFadeRange = 0.1f;

//		private Camera mainCamera;
//		private Camera reflectionCamera;
//		private Camera textureCamera;
//		[SerializeField] private Camera postProcessingCamera;
//		private RenderTexture renderTexture;
//		private Mesh effectMesh;
//		private Material effectMaterial;
//		private bool isMaterialDynamic;
//		private bool isTextureDynamic;
//		private float timeSeed;

//		// Track previous values
//		private Color lastMirrorTint;
//		private Color lastBaseColor;
//		private float lastFrostDepth;
//		private float lastNoiseStrength;
//		private float lastFilmIntensity;
//		private float lastNoiseScale;
//		private Texture2D lastNoiseTexture;
//		private float lastRippleSpeed;
//		private float lastRippleAmplitude;
//		private float lastRippleFrequency;
//		private float lastRippleOffset;
//		private float lastReflectionStrength;
//		private float lastFrostThreshold;
//		private float lastFrostFadeRange;
//		private Material lastSkyboxMaterial;

//		//void Start()
//		//{
//		//	mainCamera = GetComponent<Camera>();
//		//	if (mainCamera == null)
//		//	{
//		//		Debug.LogError("Camera component missing.", this);
//		//		enabled = false;
//		//		return;
//		//	}

//		//	if (Camera.main == mainCamera)
//		//		mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Preview"));

//		//	// ─── Reflection child setup ────────────────────────────────────────
//		//	var obj = new GameObject("ReflectionCamera");
//		//	obj.transform.SetParent(transform, false);
//		//	reflectionCamera = obj.AddComponent<Camera>();
//		//	reflectionCamera.clearFlags = CameraClearFlags.Nothing;
//		//	reflectionCamera.cullingMask = mainCamera.cullingMask;
//		//	reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

//		//	var provider = obj.AddComponent<CameraCommandProvider>();
//		//	if (provider == null)
//		//	{
//		//		Debug.LogError("Failed to add CameraCommandProvider to ReflectionCamera", this);
//		//		enabled = false;
//		//		return;
//		//	}

//		//	// Existing invert culling for reflection
//		//	provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
//		//	provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => { cmd.SetInvertCulling(false); });

//		//	// Skybox for reflection camera
//		//	provider.RegisterCommand(RenderPassEvent.BeforeRenderingSkybox, (cmd, cam) =>
//		//	{
//		//		// Clear to black first — prevents yellow garbage
//		//		cmd.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, 1f));  // black color + full alpha

//		//		Material skyMat = overrideSkyboxMaterial ?? RenderSettings.skybox;
//		//		if (skyMat == null) return;

//		//		cmd.SetInvertCulling(true);
//		//		cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
//		//		cmd.DrawProcedural(Matrix4x4.identity, skyMat, 0, MeshTopology.Triangles, 3);
//		//		cmd.SetInvertCulling(false);
//		//	});

//		//	var data = obj.AddComponent<UniversalAdditionalCameraData>();
//		//	data.renderType = CameraRenderType.Overlay;
//		//	URPCameraHelper.SetClearDepth(data, false);

//		//	// ─── NEW: Add skybox command buffer to the MAIN/BASE camera ────────
//		//	var mainProvider = mainCamera.gameObject.GetComponent<CameraCommandProvider>();
//		//	if (mainProvider == null)
//		//	{
//		//		mainProvider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();
//		//	}

//		//	// For the main/base camera provider
//		//	mainProvider.RegisterCommand(RenderPassEvent.BeforeRenderingSkybox, (cmd, cam) =>
//		//	{
//		//		// Same clear here — this is what fixes the yellow on main view
//		//		cmd.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, 1f));

//		//		Material skyMat = overrideSkyboxMaterial ?? RenderSettings.skybox;
//		//		if (skyMat == null) return;

//		//		cmd.SetInvertCulling(true);
//		//		cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
//		//		cmd.DrawProcedural(Matrix4x4.identity, skyMat, 0, MeshTopology.Triangles, 3);
//		//		cmd.SetInvertCulling(false);
//		//	});

//		//	// Stop default skybox drawing on main camera
//		//	mainCamera.clearFlags = CameraClearFlags.Nothing;

//		//	InitializeEffect();
//		//	previousEffectMode = effectMode;
//		//	StoreMaterialPropertyValues();
//		//}

//		void Start()
//		{
//			mainCamera = GetComponent<Camera>();
//			if (mainCamera == null)
//			{
//				Debug.LogError("Camera component missing.", this);
//				enabled = false;
//				return;
//			}

//			if (Camera.main == mainCamera) mainCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Preview"));//make sure to remove Preview from cullling if main camera

//			var obj = new GameObject("ReflectionCamera");
//			obj.transform.SetParent(transform, false);
//			reflectionCamera = obj.AddComponent<Camera>();
//			reflectionCamera.clearFlags = CameraClearFlags.Nothing;
//			reflectionCamera.cullingMask = mainCamera.cullingMask;
//			reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
//			//reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("TransparentFX"));//not sure

//			var provider = obj.AddComponent<CameraCommandProvider>();
//			if (provider == null)
//			{
//				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCamera", this);
//				enabled = false;
//				return;
//			}

//			provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
//			provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => { cmd.SetInvertCulling(false); });

//			//// Add this after the existing invert-culling registrations
//			//provider.RegisterCommand(RenderPassEvent.BeforeRenderingSkybox, (cmd, cam) =>
//			//{
//			//	Material skyMat = overrideSkyboxMaterial ?? RenderSettings.skybox;
//			//	if (skyMat == null) return;

//			//	cmd.SetInvertCulling(true);                // most sky shaders need this
//			//	cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
//			//	cmd.DrawProcedural(Matrix4x4.identity, skyMat, 0, MeshTopology.Triangles, 3);
//			//	cmd.SetInvertCulling(false);
//			//});

//			var data = obj.AddComponent<UniversalAdditionalCameraData>();
//			data.renderType = CameraRenderType.Overlay;
//			URPCameraHelper.SetClearDepth(data, false);

//			InitializeEffect();
//			previousEffectMode = effectMode;
//			StoreMaterialPropertyValues();

//			//mainCamera.cullingMask |= 1 << LayerMask.NameToLayer("Editor");//no need for this any more
//		}

//		private void InitializeEffect()
//		{
//			CleanupDynamicResources();

//			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
//			{
//				if (noiseTexture == null)
//				{
//					//noiseTexture = TextureUtils.GeneratePerlinNoiseTexture();
//					//noiseTexture = TextureUtils.GenerateWangTileAtlas();
//					noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
//					isTextureDynamic = true;
//				}
//				else
//				{
//					isTextureDynamic = false;
//				}
//			}
//			else
//			{
//				if (isTextureDynamic && noiseTexture != null)
//				{
//					DestroyImmediate(noiseTexture);
//					noiseTexture = null;
//					isTextureDynamic = false;
//				}
//			}

//			Camera outputStage = null;
//			switch (effectMode)
//			{
//				case EffectMode.PerfectMirror:
//					SetupRenderTexture("MirrorRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(renderTexture, mirrorTint);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					reflectionCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.SurfaceFilm:
//					SetupRenderTexture("MirrorWithFilmRT");
//					effectMesh = new Mesh();
//					//effectMaterial = MaterialUtils.CreateMirrorWithFilmOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
//					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					reflectionCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.FrostEffect:
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, renderTexture, noiseTexture, noiseStrength);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					reflectionCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.Water:
//					SetupRenderTexture("WaterRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					reflectionCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.OceanEffect:
//					SetupRenderTexture("OceanRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, renderTexture, noiseTexture);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					reflectionCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				default:
//					var defaultData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//					defaultData.cameraStack.Clear();
//					defaultData.cameraStack.Add(reflectionCamera);
//					outputStage = mainCamera;
//					break;
//			}

//			var mainCameraData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//			if (null != postProcessingCamera) mainCameraData.cameraStack.Add(postProcessingCamera);

//			UpdateMaterialProperties();
//			SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);

//			if (outputStage != null)
//			{
//				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
//				if (provider == null)
//					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

//				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
//				(cmd, cam) =>
//				{
//					if (effectMesh == null) return;
//					//FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);
//					FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh, true);
//					if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
//					{
//						effectMaterial.SetPass(0);
//						cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
//					}
//				});
//			}
//		}

//		private void SetupRenderTexture(string textureName)
//		{
//			if (renderTexture == null)
//			{
//				renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
//				{
//					name = textureName,
//					useMipMap = false,
//					autoGenerateMips = false,
//					filterMode = FilterMode.Bilinear,
//					useDynamicScale = true
//				};
//				renderTexture.Create();
//			}
//		}

//		private void SetupTextureCamera()
//		{
//			if (textureCamera == null)
//			{
//				var obj = new GameObject("TextureCamera");
//				obj.transform.SetParent(transform, false);
//				textureCamera = obj.AddComponent<Camera>();
//				textureCamera.CopyFrom(mainCamera);
//				textureCamera.clearFlags = mainCamera.clearFlags;
//				textureCamera.cullingMask = mainCamera.cullingMask;
//				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("TransparentFX"));
//				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
//				textureCamera.targetTexture = renderTexture;
//				textureCamera.depth = mainCamera.depth - 1;
//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.cameraStack.Clear();
//				data.cameraStack.Add(reflectionCamera);
//				obj.AddComponent<CameraCommandProvider>();
//			}
//		}

//		private bool HasMaterialPropertiesChanged()
//		{
//			return mirrorTint != lastMirrorTint ||
//				   baseColor != lastBaseColor ||
//				   frostDepth != lastFrostDepth ||
//				   noiseStrength != lastNoiseStrength ||
//				   filmIntensity != lastFilmIntensity ||
//				   noiseScale != lastNoiseScale ||
//				   noiseTexture != lastNoiseTexture ||
//				   rippleSpeed != lastRippleSpeed ||
//				   rippleAmplitude != lastRippleAmplitude ||
//				   rippleFrequency != lastRippleFrequency ||
//				   rippleOffset != lastRippleOffset ||
//				   reflectionStrength != lastReflectionStrength ||
//				   frostThreshold != lastFrostThreshold ||
//				   frostFadeRange != lastFrostFadeRange ||
//				   RenderSettings.skybox != lastSkyboxMaterial;
//		}

//		private void StoreMaterialPropertyValues()
//		{
//			lastMirrorTint = mirrorTint;
//			lastBaseColor = baseColor;
//			lastFrostDepth = frostDepth;
//			lastNoiseStrength = noiseStrength;
//			lastFilmIntensity = filmIntensity;
//			lastNoiseScale = noiseScale;
//			lastNoiseTexture = noiseTexture;
//			lastRippleSpeed = rippleSpeed;
//			lastRippleAmplitude = rippleAmplitude;
//			lastRippleFrequency = rippleFrequency;
//			lastRippleOffset = rippleOffset;
//			lastReflectionStrength = reflectionStrength;
//			lastFrostThreshold = frostThreshold;
//			lastFrostFadeRange = frostFadeRange;
//			lastSkyboxMaterial = RenderSettings.skybox;
//		}

//		private void UpdateMaterialProperties()
//		{
//			if (effectMaterial == null) return;

//			var skyMatToUse = overrideSkyboxMaterial ?? RenderSettings.skybox;  // fallback to global if no override
//			SkyboxUtility.SetSkyboxCubemap(effectMaterial, skyMatToUse);

//			if (HasMaterialPropertiesChanged())
//			{
//				switch (effectMode)
//				{
//					case EffectMode.PerfectMirror:
//					case EffectMode.SurfaceFilm:
//						effectMaterial.SetColor("_DimColor", mirrorTint);
//						if (effectMode == EffectMode.SurfaceFilm)
//						{
//							effectMaterial.SetFloat("_FilmIntensity", filmIntensity);
//							effectMaterial.SetFloat("_NoiseScale", noiseScale);
//							effectMaterial.SetTexture("_NoiseTex", noiseTexture);
//						}
//						break;

//					case EffectMode.FrostEffect:
//						effectMaterial.SetColor("_BaseColor", baseColor);
//						effectMaterial.SetFloat("_Depth", frostDepth);
//						effectMaterial.SetFloat("_NoiseStrength", noiseStrength);
//						effectMaterial.SetTexture("_NoiseTex", noiseTexture);
//						break;

//					case EffectMode.Water:
//						effectMaterial.SetColor("_BaseColor", baseColor);
//						effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
//						effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
//						effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
//						effectMaterial.SetFloat("_RippleOffset", rippleOffset);
//						effectMaterial.SetFloat("_ReflectionStrength", reflectionStrength);
//						if (RenderSettings.skybox != lastSkyboxMaterial)
//							SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);
//						break;

//					case EffectMode.OceanEffect:
//						effectMaterial.SetColor("_BaseColor", baseColor);
//						effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
//						effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
//						effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
//						effectMaterial.SetFloat("_RippleOffset", rippleOffset);
//						effectMaterial.SetFloat("_DepthThreshold", 128.0f);
//						effectMaterial.SetFloat("_FrostDepth", frostDepth);
//						effectMaterial.SetFloat("_FrostNoiseStrength", noiseStrength);
//						effectMaterial.SetFloat("_FrostThreshold", frostThreshold);
//						effectMaterial.SetFloat("_FrostFadeRange", frostFadeRange);
//						effectMaterial.SetTexture("_NoiseTex", noiseTexture);
//						if (RenderSettings.skybox != lastSkyboxMaterial)
//							SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);
//						break;
//				}

//				StoreMaterialPropertyValues();
//			}

//			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//			{
//				effectMaterial.SetFloat("_TimeSeed", timeSeed);
//				if (effectMode == EffectMode.OceanEffect)
//					effectMaterial.SetFloat("_RippleSeed", timeSeed);
//			}
//		}

//		public void Update()
//		{
//			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//			{
//				timeSeed += Time.deltaTime;
//			}

//			UpdateMaterialProperties();
//		}

//		private void LateUpdate()
//		{
//			if (reflectionCamera != null)
//			{
//				reflectionCamera.fieldOfView = mainCamera.fieldOfView;
//				reflectionCamera.nearClipPlane = mainCamera.nearClipPlane;
//				reflectionCamera.farClipPlane = mainCamera.farClipPlane;
//				reflectionCamera.aspect = mainCamera.aspect;
//				reflectionCamera.clearFlags = CameraClearFlags.Nothing;

//				var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
//				reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//				reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
//			}

//			if (textureCamera != null)
//			{
//				textureCamera.fieldOfView = mainCamera.fieldOfView;
//				textureCamera.nearClipPlane = mainCamera.nearClipPlane;
//				textureCamera.farClipPlane = mainCamera.farClipPlane;
//				textureCamera.aspect = mainCamera.aspect;
//				textureCamera.orthographic = mainCamera.orthographic;
//				textureCamera.orthographicSize = mainCamera.orthographicSize;
//			}
//		}

//		public void OnValidate()
//		{
//			if (!isActiveAndEnabled || mainCamera == null)
//				return;

//			if (effectMode != previousEffectMode)
//			{
//				InitializeEffect();
//				previousEffectMode = effectMode;
//			}
//			else
//			{
//				UpdateMaterialProperties();
//			}
//		}

//		//public void ForceSkyboxUpdate()
//		//{
//		//	if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//		//	{
//		//		SkyboxUtility.SetSkyboxCubemap(effectMaterial, RenderSettings.skybox);
//		//		lastSkyboxMaterial = RenderSettings.skybox;
//		//	}
//		//}

//		// Public setter (call from preview init or main camera sync)
//		//public void SetSkyboxOverride(Material skyboxMat)
//		//{
//		//	overrideSkyboxMaterial = skyboxMat;

//		//	// Immediate update if already initialized
//		//	if (effectMaterial != null && (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect))
//		//	{
//		//		SkyboxUtility.SetSkyboxCubemap(effectMaterial, skyboxMat);
//		//	}
//		//}

//		//// In SetSkyboxOverride
//		public void SetSkyboxOverride(Material skyboxMat)
//		{
//			overrideSkyboxMaterial = skyboxMat;
//		}

//		private void CleanupDynamicResources()
//		{
//			if (effectMaterial != null && isMaterialDynamic)
//			{
//				DestroyImmediate(effectMaterial);
//				effectMaterial = null;
//			}
//			if (effectMesh != null)
//			{
//				DestroyImmediate(effectMesh);
//				effectMesh = null;
//			}
//			if (noiseTexture != null && isTextureDynamic)
//			{
//				DestroyImmediate(noiseTexture);
//				noiseTexture = null;
//			}
//			if (renderTexture != null && effectMode != EffectMode.FrostEffect && effectMode != EffectMode.Water && effectMode != EffectMode.OceanEffect)
//			{
//				DestroyImmediate(renderTexture);
//				renderTexture = null;
//			}
//			if (textureCamera != null && effectMode != EffectMode.FrostEffect && effectMode != EffectMode.Water && effectMode != EffectMode.OceanEffect)
//			{
//				DestroyImmediate(textureCamera.gameObject);
//				textureCamera = null;
//			}
//		}

//		void OnDestroy()
//		{
//			if (mainCamera != null) mainCamera.targetTexture = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;
//			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
//			if (effectMesh != null) DestroyImmediate(effectMesh);
//			if (renderTexture != null) DestroyImmediate(renderTexture);
//			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);
//		}
//	}
//}