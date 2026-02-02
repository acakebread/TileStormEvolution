
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

		[SerializeField] private EffectMode effectMode = EffectMode.Water;
		[SerializeField, HideInInspector] private EffectMode previousEffectMode;
		public void SetEffectMode(EffectMode value)
		{
			effectMode = value;
			if (value == EffectMode.Water)
				ApplyWaterDefault();
		}

		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
		[SerializeField] private Texture2D noiseTexture;

		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

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

		[Header("Preview Mode (internal)")]
		[SerializeField, HideInInspector] private bool isPreviewCamera = false;
		public void SetPreviewMode(bool value) => isPreviewCamera = value;

		private Material overrideSkyboxMaterial;
		public void SetSkyboxOverride(Material value) => overrideSkyboxMaterial = value;
		private Material activeSkybox => overrideSkyboxMaterial != null ? overrideSkyboxMaterial : RenderSettings.skybox;

		void Awake()
		{
			mainCamera = GetComponent<Camera>();
			if (mainCamera == null)
			{
				Debug.LogError("Camera component missing.", this);
				enabled = false;
				return;
			}
			if (Camera.main == mainCamera)
				PreviewRenderLayers.RemovePreviewLayers(mainCamera);

			CleanupOrphanedChildren();

			if (reflectionCamera == null)
			{
				var obj = new GameObject("ReflectionCamera");
				obj.transform.SetParent(transform, false);
				reflectionCamera = obj.AddComponent<Camera>();
				reflectionCamera.clearFlags = CameraClearFlags.Nothing;
				reflectionCamera.cullingMask = mainCamera.cullingMask;
				reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

				var provider = obj.AddComponent<CameraCommandProvider>();
				provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
				provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

				var data = obj.AddComponent<UniversalAdditionalCameraData>();
				data.renderType = CameraRenderType.Overlay;
				URPCameraHelper.SetClearDepth(data, false);
			}
			InitializeEffect();
			previousEffectMode = effectMode;
			StoreMaterialPropertyValues();
		}

		private void InitializeEffect()
		{
			CleanupDynamicResources();

			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
			{
				if (noiseTexture == null)
				{
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
					// Pass null for reflection texture — set later
					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(null, mirrorTint);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					outputStage = mainCamera;
					break;

				case EffectMode.SurfaceFilm:
					SetupRenderTexture("MirrorWithFilmRT");
					effectMesh = new Mesh();
					// Pass null for reflection texture — set later
					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(null, mirrorTint, noiseTexture, filmIntensity, noiseScale);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					outputStage = mainCamera;
					break;

				case EffectMode.FrostEffect:
					SetupRenderTexture("RenderTexture");
					effectMesh = new Mesh();
					// Pass null for reflection texture — set later
					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, null, noiseTexture, noiseStrength);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					outputStage = mainCamera;
					break;

				case EffectMode.Water:
					SetupRenderTexture("WaterRenderTexture");
					effectMesh = new Mesh();
					// Pass null for reflection texture — set later
					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, null, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					outputStage = mainCamera;
					break;

				case EffectMode.OceanEffect:
					SetupRenderTexture("OceanRenderTexture");
					effectMesh = new Mesh();
					// Pass null for reflection texture — set later
					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, null, noiseTexture);
					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
			if (postProcessingCamera != null) mainCameraData.cameraStack.Add(postProcessingCamera);

			UpdateMaterialProperties();
			SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);

			if (outputStage != null)
			{
				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
				if (provider == null)
					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
				(cmd, cam) =>
				{
					if (effectMesh == null) return;
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
				textureCamera.cullingMask &= ~(1 << PreviewRenderLayers.previewTransparentLayer);
				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
				textureCamera.targetTexture = renderTexture;
				textureCamera.depth = mainCamera.depth - 1;
				var data = obj.AddComponent<UniversalAdditionalCameraData>();
				data.cameraStack.Clear();
				data.cameraStack.Add(reflectionCamera);
				obj.AddComponent<CameraCommandProvider>();

				if (isPreviewCamera)
				{
					var ambientOverride = textureCamera.gameObject.AddComponent<ClassicTilestorm.PreviewAmbientOverride>();
					Debug.Log($"PreviewAmbientOverride attached to TextureCamera (preview only) - ToDo: get rid of this hack!!!");
				}
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
				   activeSkybox != lastSkyboxMaterial;
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
			lastSkyboxMaterial = activeSkybox;
		}

		private void UpdateMaterialProperties()
		{
			if (effectMaterial == null) return;

			if (HasMaterialPropertiesChanged())
			{
				// Lazy one-time assignment of reflection RT to material (after RT is created & camera is rendering)
				if (renderTexture != null && effectMaterial.HasProperty("_MainTex") && effectMaterial.GetTexture("_MainTex") == null)
				{
					effectMaterial.SetTexture("_MainTex", renderTexture);
				}

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
						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
			baseColor = new Color(0, 0, 0, 0.6f);
			rippleSpeed = 0.25f;
			rippleAmplitude = 0.15f;
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
				previousEffectMode = effectMode;
			}
			else if (effectMaterial != null)
			{
				UpdateMaterialProperties();
			}
		}

		public void ForceSkyboxUpdate()
		{
			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
			{
				lastSkyboxMaterial = activeSkybox;
				SkyboxUtility.SetSkyboxCubemap(effectMaterial, lastSkyboxMaterial);
			}
		}

		private void CleanupDynamicResources()
		{
			RenderTexture.active = null;
			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
			if (textureCamera != null) textureCamera.targetTexture = null;

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
			if (renderTexture != null)
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

		private void CleanupOrphanedChildren()
		{
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				var child = transform.GetChild(i);
				if (child.name == "ReflectionCamera" || child.name == "TextureCamera")
				{
					if (child.GetComponent<Camera>() != null)
					{
						DestroyImmediate(child.gameObject);
					}
				}
			}
		}

		void OnDestroy()
		{
			RenderTexture.active = null;
			if (mainCamera != null) mainCamera.targetTexture = null;
			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
			if (textureCamera != null) textureCamera.targetTexture = null;

			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
			if (effectMesh != null) DestroyImmediate(effectMesh);
			if (renderTexture != null) DestroyImmediate(renderTexture);
			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);

			CleanupOrphanedChildren();
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
//		public void SetEffectMode(EffectMode value)
//		{
//			effectMode = value;
//			if (value == EffectMode.Water)
//				ApplyWaterDefault();
//		}

//		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
//		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

//		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
//		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

//		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
//		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
//		[SerializeField] private Texture2D noiseTexture;

//		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

//		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

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

//		[Header("Preview Mode (internal)")]
//		[SerializeField, HideInInspector] private bool isPreviewCamera = false;
//		public void SetPreviewMode(bool value) => isPreviewCamera = value;

//		private Material activeSkybox => overrideSkyboxMaterial != null ? overrideSkyboxMaterial : RenderSettings.skybox;

//		private Material overrideSkyboxMaterial;

//		void Awake()
//		{
//			mainCamera = GetComponent<Camera>();
//			if (mainCamera == null)
//			{
//				Debug.LogError("Camera component missing.", this);
//				enabled = false;
//				return;
//			}
//			if (Camera.main == mainCamera)
//				PreviewRenderLayers.RemovePreviewLayers(mainCamera);

//			CleanupOrphanedChildren();

//			if (reflectionCamera == null)
//			{
//				var obj = new GameObject("ReflectionCamera");
//				obj.transform.SetParent(transform, false);
//				reflectionCamera = obj.AddComponent<Camera>();
//				reflectionCamera.clearFlags = CameraClearFlags.Nothing;
//				reflectionCamera.cullingMask = mainCamera.cullingMask;
//				reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

//				var provider = obj.AddComponent<CameraCommandProvider>();
//				provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
//				provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.renderType = CameraRenderType.Overlay;
//				URPCameraHelper.SetClearDepth(data, false);
//			}
//		}

//		void Start()
//		{
//			InitializeEffect();
//			AssignRenderTextures();  // NEW: safe RT creation & assignment in Start
//			previousEffectMode = effectMode;
//			StoreMaterialPropertyValues();
//		}

//		public void SetSkyboxOverride(Material value)
//		{
//			overrideSkyboxMaterial = value;
//		}

//		private void InitializeEffect()
//		{
//			CleanupDynamicResources();

//			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
//			{
//				if (noiseTexture == null)
//				{
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
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(renderTexture, mirrorTint);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					outputStage = mainCamera;
//					break;

//				case EffectMode.SurfaceFilm:
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					outputStage = mainCamera;
//					break;

//				case EffectMode.FrostEffect:
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, renderTexture, noiseTexture, noiseStrength);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					outputStage = mainCamera;
//					break;

//				case EffectMode.Water:
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					outputStage = mainCamera;
//					break;

//				case EffectMode.OceanEffect:
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, renderTexture, noiseTexture);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
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
//			if (postProcessingCamera != null) mainCameraData.cameraStack.Add(postProcessingCamera);

//			UpdateMaterialProperties();
//			SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);

//			if (outputStage != null)
//			{
//				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
//				if (provider == null)
//					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

//				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
//				(cmd, cam) =>
//				{
//					if (effectMesh == null) return;
//					FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh, true);
//					if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
//					{
//						effectMaterial.SetPass(0);
//						cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
//					}
//				});
//			}
//		}

//		private void AssignRenderTextures()
//		{
//			string textureName = effectMode switch
//			{
//				EffectMode.PerfectMirror => "RenderTexture",
//				EffectMode.SurfaceFilm => "MirrorWithFilmRT",
//				EffectMode.FrostEffect => "RenderTexture",
//				EffectMode.Water => "WaterRenderTexture",
//				EffectMode.OceanEffect => "OceanRenderTexture",
//				_ => "RenderTexture"
//			};

//			SetupRenderTexture(textureName);

//			reflectionCamera.targetTexture = renderTexture;

//			//if (textureCamera != null &&
//			//	(effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect ||
//			//	 effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect))
//			{
//				textureCamera.targetTexture = renderTexture;
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
//				textureCamera.cullingMask &= ~(1 << PreviewRenderLayers.previewTransparentLayer);
//				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
//				textureCamera.targetTexture = renderTexture;
//				textureCamera.depth = mainCamera.depth - 1;
//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.cameraStack.Clear();
//				data.cameraStack.Add(reflectionCamera);
//				obj.AddComponent<CameraCommandProvider>();

//				if (isPreviewCamera)
//				{
//					var ambientOverride = textureCamera.gameObject.AddComponent<ClassicTilestorm.PreviewAmbientOverride>();
//					Debug.Log($"PreviewAmbientOverride attached to TextureCamera (preview only) - ToDo: get rid of this hack!!!");
//				}
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
//				   activeSkybox != lastSkyboxMaterial;
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
//			lastSkyboxMaterial = activeSkybox;
//		}

//		private void UpdateMaterialProperties()
//		{
//			if (effectMaterial == null) return;

//			if (renderTexture != null && effectMaterial.HasProperty("_MainTex") && effectMaterial.GetTexture("_MainTex") == null)
//			{
//				effectMaterial.SetTexture("_MainTex", renderTexture);
//				// Optional debug to confirm
//				// Debug.Log($"Assigned reflection RT to {effectMode} material on frame {Time.frameCount}");
//			}

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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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

//		public void ApplyWaterDefault()
//		{
//			baseColor = new Color(0, 0, 0, 0.5f);
//			rippleSpeed = 0.25f;
//			rippleAmplitude = 0.25f;
//			rippleFrequency = 0.35f;
//			reflectionStrength = 0.5f;
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
//				previousEffectMode = effectMode;
//			}
//			else if (effectMaterial != null)
//			{
//				UpdateMaterialProperties();
//			}
//		}

//		public void ForceSkyboxUpdate()
//		{
//			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//			{
//				lastSkyboxMaterial = activeSkybox;
//				SkyboxUtility.SetSkyboxCubemap(effectMaterial, lastSkyboxMaterial);
//			}
//		}

//		private void CleanupDynamicResources()
//		{
//			RenderTexture.active = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

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
//			if (renderTexture != null)
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

//		private void CleanupOrphanedChildren()
//		{
//			for (int i = transform.childCount - 1; i >= 0; i--)
//			{
//				var child = transform.GetChild(i);
//				if (child.name == "ReflectionCamera" || child.name == "TextureCamera")
//				{
//					if (child.GetComponent<Camera>() != null)
//					{
//						DestroyImmediate(child.gameObject);
//					}
//				}
//			}
//		}

//		void OnDestroy()
//		{
//			RenderTexture.active = null;
//			if (mainCamera != null) mainCamera.targetTexture = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

//			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
//			if (effectMesh != null) DestroyImmediate(effectMesh);
//			if (renderTexture != null) DestroyImmediate(renderTexture);
//			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);

//			CleanupOrphanedChildren();
//		}
//	}
//}


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
//		public void SetEffectMode(EffectMode value)
//		{
//			effectMode = value;
//			if (value == EffectMode.Water)
//				ApplyWaterDefault();
//		}

//		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
//		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

//		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
//		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

//		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
//		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
//		[SerializeField] private Texture2D noiseTexture;

//		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

//		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

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

//		[Header("Preview Mode (internal)")]
//		[SerializeField, HideInInspector] private bool isPreviewCamera = false;
//		public void SetPreviewMode(bool value) => isPreviewCamera = value;

//		private Material activeSkybox => overrideSkyboxMaterial != null ? overrideSkyboxMaterial : RenderSettings.skybox;

//		private Material overrideSkyboxMaterial;

//		void Awake()
//		{
//			mainCamera = GetComponent<Camera>();
//			if (mainCamera == null)
//			{
//				Debug.LogError("Camera component missing.", this);
//				enabled = false;
//				return;
//			}
//			if (Camera.main == mainCamera)
//				PreviewRenderLayers.RemovePreviewLayers(mainCamera);

//			CleanupOrphanedChildren();

//			if (reflectionCamera == null)
//			{
//				var obj = new GameObject("ReflectionCamera");
//				obj.transform.SetParent(transform, false);
//				reflectionCamera = obj.AddComponent<Camera>();
//				reflectionCamera.clearFlags = CameraClearFlags.Nothing;
//				reflectionCamera.cullingMask = mainCamera.cullingMask;
//				reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

//				var provider = obj.AddComponent<CameraCommandProvider>();
//				provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
//				provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.renderType = CameraRenderType.Overlay;
//				URPCameraHelper.SetClearDepth(data, false);
//			}

//			// Create + setup TextureCamera early for ALL modes that originally called SetupTextureCamera()
//			bool needsTextureCamera = effectMode == EffectMode.PerfectMirror ||
//									  effectMode == EffectMode.SurfaceFilm ||
//									  effectMode == EffectMode.FrostEffect ||
//									  effectMode == EffectMode.Water ||
//									  effectMode == EffectMode.OceanEffect;

//			if (needsTextureCamera && textureCamera == null)
//			{
//				SetupTextureCamera();  // Full setup — no RT assignment here
//			}
//		}

//		void Start()
//		{
//			InitializeEffect();
//			previousEffectMode = effectMode;
//			StoreMaterialPropertyValues();
//		}

//		public void SetSkyboxOverride(Material value)
//		{
//			overrideSkyboxMaterial = value;
//		}

//		private void InitializeEffect()
//		{
//			CleanupDynamicResources();

//			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
//			{
//				if (noiseTexture == null)
//				{
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
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(renderTexture, mirrorTint);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.SurfaceFilm:
//					SetupRenderTexture("MirrorWithFilmRT");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.FrostEffect:
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, renderTexture, noiseTexture, noiseStrength);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.Water:
//					SetupRenderTexture("WaterRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.OceanEffect:
//					SetupRenderTexture("OceanRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, renderTexture, noiseTexture);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				default:
//					var defaultData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//					defaultData.cameraStack.Clear();
//					defaultData.cameraStack.Add(reflectionCamera);
//					outputStage = mainCamera;
//					break;
//			}

//			// Single RT assignment
//			reflectionCamera.targetTexture = renderTexture;

//			// Assign to TextureCamera ONLY for modes that need the extra render pass
//			if (textureCamera != null &&
//				(effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect ||
//				 effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect))
//			{
//				textureCamera.targetTexture = renderTexture;
//			}

//			var mainCameraData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//			if (postProcessingCamera != null) mainCameraData.cameraStack.Add(postProcessingCamera);

//			UpdateMaterialProperties();
//			SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);

//			if (outputStage != null)
//			{
//				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
//				if (provider == null)
//					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

//				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
//				(cmd, cam) =>
//				{
//					if (effectMesh == null) return;
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
//			if (textureCamera != null) return;

//			var obj = new GameObject("TextureCamera");
//			obj.transform.SetParent(transform, false);
//			textureCamera = obj.AddComponent<Camera>();
//			textureCamera.CopyFrom(mainCamera);
//			textureCamera.clearFlags = mainCamera.clearFlags;
//			textureCamera.cullingMask = mainCamera.cullingMask;
//			textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("TransparentFX"));
//			textureCamera.cullingMask &= ~(1 << PreviewRenderLayers.previewTransparentLayer);
//			textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
//			// REMOVED: textureCamera.targetTexture = renderTexture; — moved to InitializeEffect
//			textureCamera.depth = mainCamera.depth - 1;

//			var data = obj.AddComponent<UniversalAdditionalCameraData>();
//			data.cameraStack.Clear();
//			data.cameraStack.Add(reflectionCamera);
//			obj.AddComponent<CameraCommandProvider>();

//			if (isPreviewCamera)
//			{
//				var ambientOverride = textureCamera.gameObject.AddComponent<ClassicTilestorm.PreviewAmbientOverride>();
//				Debug.Log($"PreviewAmbientOverride attached to TextureCamera (preview only) - ToDo: get rid of this hack!!!");
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
//				   activeSkybox != lastSkyboxMaterial;
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
//			lastSkyboxMaterial = activeSkybox;
//		}

//		private void UpdateMaterialProperties()
//		{
//			if (effectMaterial == null) return;

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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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

//		public void ApplyWaterDefault()
//		{
//			baseColor = new Color(0, 0, 0, 0.5f);
//			rippleSpeed = 0.25f;
//			rippleAmplitude = 0.25f;
//			rippleFrequency = 0.35f;
//			reflectionStrength = 0.5f;
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
//				previousEffectMode = effectMode;
//			}
//			else if (effectMaterial != null)
//			{
//				UpdateMaterialProperties();
//			}
//		}

//		public void ForceSkyboxUpdate()
//		{
//			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//			{
//				lastSkyboxMaterial = activeSkybox;
//				SkyboxUtility.SetSkyboxCubemap(effectMaterial, lastSkyboxMaterial);
//			}
//		}

//		private void CleanupDynamicResources()
//		{
//			RenderTexture.active = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

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
//			if (renderTexture != null)
//			{
//				DestroyImmediate(renderTexture);
//				renderTexture = null;
//			}
//			// Do NOT destroy textureCamera here — created early in Awake
//		}

//		private void CleanupOrphanedChildren()
//		{
//			for (int i = transform.childCount - 1; i >= 0; i--)
//			{
//				var child = transform.GetChild(i);
//				if (child.name == "ReflectionCamera" || child.name == "TextureCamera")
//				{
//					if (child.GetComponent<Camera>() != null)
//					{
//						DestroyImmediate(child.gameObject);
//					}
//				}
//			}
//		}

//		void OnDestroy()
//		{
//			RenderTexture.active = null;
//			if (mainCamera != null) mainCamera.targetTexture = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

//			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
//			if (effectMesh != null) DestroyImmediate(effectMesh);
//			if (renderTexture != null) DestroyImmediate(renderTexture);
//			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);

//			CleanupOrphanedChildren();
//		}
//	}
//}

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
//		public void SetEffectMode(EffectMode value)
//		{
//			effectMode = value;
//			if (value == EffectMode.Water)
//				ApplyWaterDefault();
//		}

//		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
//		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

//		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
//		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

//		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
//		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
//		[SerializeField] private Texture2D noiseTexture;

//		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

//		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

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

//		[Header("Preview Mode (internal)")]
//		[SerializeField, HideInInspector] private bool isPreviewCamera = false;
//		public void SetPreviewMode(bool value) => isPreviewCamera = value;

//		private Material activeSkybox => overrideSkyboxMaterial != null ? overrideSkyboxMaterial : RenderSettings.skybox;

//		private Material overrideSkyboxMaterial;

//		void Awake()
//		{
//			mainCamera = GetComponent<Camera>();
//			if (mainCamera == null)
//			{
//				Debug.LogError("Camera component missing.", this);
//				enabled = false;
//				return;
//			}
//			if (Camera.main == mainCamera)
//				PreviewRenderLayers.RemovePreviewLayers(mainCamera);

//			CleanupOrphanedChildren();

//			if (reflectionCamera == null)
//			{
//				var obj = new GameObject("ReflectionCamera");
//				obj.transform.SetParent(transform, false);
//				reflectionCamera = obj.AddComponent<Camera>();
//				reflectionCamera.clearFlags = CameraClearFlags.Nothing;
//				reflectionCamera.cullingMask = mainCamera.cullingMask;
//				reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

//				var provider = obj.AddComponent<CameraCommandProvider>();
//				provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
//				provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.renderType = CameraRenderType.Overlay;
//				URPCameraHelper.SetClearDepth(data, false);
//			}

//			// Create + setup TextureCamera early for ALL modes that originally called SetupTextureCamera()
//			bool needsTextureCamera = effectMode == EffectMode.PerfectMirror ||
//									  effectMode == EffectMode.SurfaceFilm ||
//									  effectMode == EffectMode.FrostEffect ||
//									  effectMode == EffectMode.Water ||
//									  effectMode == EffectMode.OceanEffect;

//			if (needsTextureCamera && textureCamera == null)
//			{
//				SetupTextureCamera();  // Calls the full setup — no RT assignment here
//			}
//		}

//		void Start()
//		{
//			InitializeEffect();
//			previousEffectMode = effectMode;
//			StoreMaterialPropertyValues();
//		}

//		public void SetSkyboxOverride(Material value)
//		{
//			overrideSkyboxMaterial = value;
//		}

//		private void InitializeEffect()
//		{
//			CleanupDynamicResources();

//			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
//			{
//				if (noiseTexture == null)
//				{
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
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(renderTexture, mirrorTint);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.SurfaceFilm:
//					SetupRenderTexture("MirrorWithFilmRT");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.FrostEffect:
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, renderTexture, noiseTexture, noiseStrength);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.Water:
//					SetupRenderTexture("WaterRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.OceanEffect:
//					SetupRenderTexture("OceanRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, renderTexture, noiseTexture);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				default:
//					var defaultData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//					defaultData.cameraStack.Clear();
//					defaultData.cameraStack.Add(reflectionCamera);
//					outputStage = mainCamera;
//					break;
//			}

//			// Single RT assignment point
//			reflectionCamera.targetTexture = renderTexture;

//			// Assign to TextureCamera ONLY for modes that use the extra render pass
//			if (textureCamera != null &&
//				(effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect ||
//				 effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect))
//			{
//				textureCamera.targetTexture = renderTexture;
//			}

//			var mainCameraData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//			if (postProcessingCamera != null) mainCameraData.cameraStack.Add(postProcessingCamera);

//			UpdateMaterialProperties();
//			SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);

//			if (outputStage != null)
//			{
//				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
//				if (provider == null)
//					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

//				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
//				(cmd, cam) =>
//				{
//					if (effectMesh == null) return;
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
//			if (textureCamera != null) return;

//			var obj = new GameObject("TextureCamera");
//			obj.transform.SetParent(transform, false);
//			textureCamera = obj.AddComponent<Camera>();
//			textureCamera.CopyFrom(mainCamera);
//			textureCamera.clearFlags = mainCamera.clearFlags;
//			textureCamera.cullingMask = mainCamera.cullingMask;
//			textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("TransparentFX"));
//			textureCamera.cullingMask &= ~(1 << PreviewRenderLayers.previewTransparentLayer);
//			textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
//			// REMOVED: textureCamera.targetTexture = renderTexture;  — moved to InitializeEffect
//			textureCamera.depth = mainCamera.depth - 1;

//			var data = obj.AddComponent<UniversalAdditionalCameraData>();
//			data.cameraStack.Clear();
//			data.cameraStack.Add(reflectionCamera);
//			obj.AddComponent<CameraCommandProvider>();

//			if (isPreviewCamera)
//			{
//				var ambientOverride = textureCamera.gameObject.AddComponent<ClassicTilestorm.PreviewAmbientOverride>();
//				Debug.Log($"PreviewAmbientOverride attached to TextureCamera (preview only) - ToDo: get rid of this hack!!!");
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
//				   activeSkybox != lastSkyboxMaterial;
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
//			lastSkyboxMaterial = activeSkybox;
//		}

//		private void UpdateMaterialProperties()
//		{
//			if (effectMaterial == null) return;

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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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

//		public void ApplyWaterDefault()
//		{
//			baseColor = new Color(0, 0, 0, 0.5f);
//			rippleSpeed = 0.25f;
//			rippleAmplitude = 0.25f;
//			rippleFrequency = 0.35f;
//			reflectionStrength = 0.5f;
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
//				previousEffectMode = effectMode;
//			}
//			else if (effectMaterial != null)
//			{
//				UpdateMaterialProperties();
//			}
//		}

//		public void ForceSkyboxUpdate()
//		{
//			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//			{
//				lastSkyboxMaterial = activeSkybox;
//				SkyboxUtility.SetSkyboxCubemap(effectMaterial, lastSkyboxMaterial);
//			}
//		}

//		private void CleanupDynamicResources()
//		{
//			RenderTexture.active = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

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
//			if (renderTexture != null)
//			{
//				DestroyImmediate(renderTexture);
//				renderTexture = null;
//			}
//			// Do NOT destroy textureCamera here — created in Awake for some modes
//		}

//		private void CleanupOrphanedChildren()
//		{
//			for (int i = transform.childCount - 1; i >= 0; i--)
//			{
//				var child = transform.GetChild(i);
//				if (child.name == "ReflectionCamera" || child.name == "TextureCamera")
//				{
//					if (child.GetComponent<Camera>() != null)
//					{
//						DestroyImmediate(child.gameObject);
//					}
//				}
//			}
//		}

//		void OnDestroy()
//		{
//			RenderTexture.active = null;
//			if (mainCamera != null) mainCamera.targetTexture = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

//			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
//			if (effectMesh != null) DestroyImmediate(effectMesh);
//			if (renderTexture != null) DestroyImmediate(renderTexture);
//			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);

//			CleanupOrphanedChildren();
//		}
//	}
//}

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
//		public void SetEffectMode(EffectMode value)
//		{
//			effectMode = value;
//			if (value == EffectMode.Water)
//				ApplyWaterDefault();
//		}

//		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
//		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

//		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
//		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

//		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
//		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
//		[SerializeField] private Texture2D noiseTexture;

//		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

//		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

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

//		[Header("Preview Mode (internal)")]
//		[SerializeField, HideInInspector] private bool isPreviewCamera = false;
//		public void SetPreviewMode(bool value) => isPreviewCamera = value;

//		private Material activeSkybox => overrideSkyboxMaterial != null ? overrideSkyboxMaterial : RenderSettings.skybox;

//		private Material overrideSkyboxMaterial;

//		void Awake()
//		{
//			mainCamera = GetComponent<Camera>();
//			if (mainCamera == null)
//			{
//				Debug.LogError("Camera component missing.", this);
//				enabled = false;
//				return;
//			}
//			if (Camera.main == mainCamera)
//				PreviewRenderLayers.RemovePreviewLayers(mainCamera);

//			CleanupOrphanedChildren();

//			if (reflectionCamera == null)
//			{
//				var obj = new GameObject("ReflectionCamera");
//				obj.transform.SetParent(transform, false);
//				reflectionCamera = obj.AddComponent<Camera>();
//				reflectionCamera.clearFlags = CameraClearFlags.Nothing;
//				reflectionCamera.cullingMask = mainCamera.cullingMask;
//				reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

//				var provider = obj.AddComponent<CameraCommandProvider>();
//				provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
//				provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.renderType = CameraRenderType.Overlay;
//				URPCameraHelper.SetClearDepth(data, false);
//			}

//			// Create texture camera early for modes that need it (Water/Ocean/SurfaceFilm/Frost)
//			// But do NOT assign targetTexture yet — that's done after RT creation in Start
//			if (textureCamera == null &&
//				(effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect ||
//				 effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect))
//			{
//				var obj = new GameObject("TextureCamera");
//				obj.transform.SetParent(transform, false);
//				textureCamera = obj.AddComponent<Camera>();
//				textureCamera.CopyFrom(mainCamera);
//				textureCamera.clearFlags = mainCamera.clearFlags;
//				textureCamera.cullingMask = mainCamera.cullingMask;
//				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("TransparentFX"));
//				textureCamera.cullingMask &= ~(1 << PreviewRenderLayers.previewTransparentLayer);
//				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
//				textureCamera.depth = mainCamera.depth - 1;

//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.cameraStack.Clear();
//				data.cameraStack.Add(reflectionCamera);
//				obj.AddComponent<CameraCommandProvider>();

//				if (isPreviewCamera)
//				{
//					var ambientOverride = textureCamera.gameObject.AddComponent<ClassicTilestorm.PreviewAmbientOverride>();
//					Debug.Log($"PreviewAmbientOverride attached to TextureCamera (preview only) - ToDo: get rid of this hack!!!");
//				}
//			}
//		}

//		void Start()
//		{
//			InitializeEffect();
//			previousEffectMode = effectMode;
//			StoreMaterialPropertyValues();
//		}

//		public void SetSkyboxOverride(Material value)
//		{
//			overrideSkyboxMaterial = value;
//		}

//		private void InitializeEffect()
//		{
//			CleanupDynamicResources();

//			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
//			{
//				if (noiseTexture == null)
//				{
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
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(renderTexture, mirrorTint);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.SurfaceFilm:
//					SetupRenderTexture("MirrorWithFilmRT");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.FrostEffect:
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, renderTexture, noiseTexture, noiseStrength);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.Water:
//					SetupRenderTexture("WaterRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.OceanEffect:
//					SetupRenderTexture("OceanRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, renderTexture, noiseTexture);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					outputStage = mainCamera;
//					break;

//				default:
//					var defaultData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//					defaultData.cameraStack.Clear();
//					defaultData.cameraStack.Add(reflectionCamera);
//					outputStage = mainCamera;
//					break;
//			}

//			// ── Single place for RT assignment ─────────────────────────────────────
//			reflectionCamera.targetTexture = renderTexture;

//			// Only assign to texture camera if it exists and the mode needs it
//			if (textureCamera != null &&
//				(effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect ||
//				 effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect))
//			{
//				textureCamera.targetTexture = renderTexture;
//			}

//			var mainCameraData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//			if (postProcessingCamera != null) mainCameraData.cameraStack.Add(postProcessingCamera);

//			UpdateMaterialProperties();
//			SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);

//			if (outputStage != null)
//			{
//				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
//				if (provider == null)
//					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

//				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
//				(cmd, cam) =>
//				{
//					if (effectMesh == null) return;
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
//				   activeSkybox != lastSkyboxMaterial;
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
//			lastSkyboxMaterial = activeSkybox;
//		}

//		private void UpdateMaterialProperties()
//		{
//			if (effectMaterial == null) return;

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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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

//		public void ApplyWaterDefault()
//		{
//			baseColor = new Color(0, 0, 0, 0.5f);
//			rippleSpeed = 0.25f;
//			rippleAmplitude = 0.25f;
//			rippleFrequency = 0.35f;
//			reflectionStrength = 0.5f;
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
//				previousEffectMode = effectMode;
//			}
//			else if (effectMaterial != null)
//			{
//				UpdateMaterialProperties();
//			}
//		}

//		public void ForceSkyboxUpdate()
//		{
//			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//			{
//				lastSkyboxMaterial = activeSkybox;
//				SkyboxUtility.SetSkyboxCubemap(effectMaterial, lastSkyboxMaterial);
//			}
//		}

//		private void CleanupDynamicResources()
//		{
//			RenderTexture.active = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

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
//			if (renderTexture != null)
//			{
//				DestroyImmediate(renderTexture);
//				renderTexture = null;
//			}
//			// Do NOT destroy textureCamera here — it's now created in Awake for some modes
//		}

//		private void CleanupOrphanedChildren()
//		{
//			for (int i = transform.childCount - 1; i >= 0; i--)
//			{
//				var child = transform.GetChild(i);
//				if (child.name == "ReflectionCamera" || child.name == "TextureCamera")
//				{
//					if (child.GetComponent<Camera>() != null)
//					{
//						DestroyImmediate(child.gameObject);
//					}
//				}
//			}
//		}

//		void OnDestroy()
//		{
//			RenderTexture.active = null;
//			if (mainCamera != null) mainCamera.targetTexture = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

//			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
//			if (effectMesh != null) DestroyImmediate(effectMesh);
//			if (renderTexture != null) DestroyImmediate(renderTexture);
//			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);

//			CleanupOrphanedChildren();
//		}
//	}
//}

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
//		public void SetEffectMode(EffectMode value)
//		{
//			effectMode = value;
//			if (value == EffectMode.Water)
//				ApplyWaterDefault();
//		}

//		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
//		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

//		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
//		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

//		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
//		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
//		[SerializeField] private Texture2D noiseTexture;

//		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

//		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

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

//		//temporary workaround
//		[Header("Preview Mode (internal)")]
//		[SerializeField, HideInInspector] private bool isPreviewCamera = false;
//		public void SetPreviewMode(bool value) => isPreviewCamera = value;

//		private Material activeSkybox => overrideSkyboxMaterial != null ? overrideSkyboxMaterial : RenderSettings.skybox;

//		void Awake()
//		{
//			mainCamera = GetComponent<Camera>();
//			if (mainCamera == null)
//			{
//				Debug.LogError("Camera component missing.", this);
//				enabled = false;
//				return;
//			}
//			if (Camera.main == mainCamera)
//				PreviewRenderLayers.RemovePreviewLayers(mainCamera);

//			CleanupOrphanedChildren();

//			if (reflectionCamera == null)
//			{
//				var obj = new GameObject("ReflectionCamera");
//				obj.transform.SetParent(transform, false);
//				reflectionCamera = obj.AddComponent<Camera>();
//				reflectionCamera.clearFlags = CameraClearFlags.Nothing;
//				reflectionCamera.cullingMask = mainCamera.cullingMask;
//				reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

//				var provider = obj.AddComponent<CameraCommandProvider>();
//				provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
//				provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.renderType = CameraRenderType.Overlay;
//				URPCameraHelper.SetClearDepth(data, false);
//			}

//			// Create texture camera early for modes that need it (Water/Ocean/SurfaceFilm/Frost)
//			// But do NOT assign targetTexture yet — that's done after RT creation in Start
//			if (textureCamera == null &&
//				(effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect ||
//				 effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect))
//			{
//				var obj = new GameObject("TextureCamera");
//				obj.transform.SetParent(transform, false);
//				textureCamera = obj.AddComponent<Camera>();
//				textureCamera.CopyFrom(mainCamera);
//				textureCamera.clearFlags = mainCamera.clearFlags;
//				textureCamera.cullingMask = mainCamera.cullingMask;
//				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("TransparentFX"));
//				textureCamera.cullingMask &= ~(1 << PreviewRenderLayers.previewTransparentLayer);
//				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
//				textureCamera.depth = mainCamera.depth - 1;

//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.cameraStack.Clear();
//				data.cameraStack.Add(reflectionCamera);
//				obj.AddComponent<CameraCommandProvider>();

//				if (isPreviewCamera)
//				{
//					var ambientOverride = textureCamera.gameObject.AddComponent<ClassicTilestorm.PreviewAmbientOverride>();
//					Debug.Log($"PreviewAmbientOverride attached to TextureCamera (preview only) - ToDo: get rid of this hack!!!");
//				}
//			}
//		}

//		void Start()
//		{
//			InitializeEffect();
//			previousEffectMode = effectMode;
//			StoreMaterialPropertyValues();
//		}

//		private Material overrideSkyboxMaterial;
//		public void SetSkyboxOverride(Material value)
//		{
//			overrideSkyboxMaterial = value;
//		}

//		private void InitializeEffect()
//		{
//			CleanupDynamicResources();

//			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
//			{
//				if (noiseTexture == null)
//				{
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
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(renderTexture, mirrorTint);
//					isMaterialDynamic = true;
//					reflectionCamera.targetTexture = renderTexture;
//					if (textureCamera != null) textureCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.SurfaceFilm:
//					SetupRenderTexture("MirrorWithFilmRT");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
//					isMaterialDynamic = true;
//					reflectionCamera.targetTexture = renderTexture;
//					if (textureCamera != null) textureCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.FrostEffect:
//					SetupRenderTexture("RenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, renderTexture, noiseTexture, noiseStrength);
//					isMaterialDynamic = true;
//					reflectionCamera.targetTexture = renderTexture;
//					if (textureCamera != null) textureCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.Water:
//					SetupRenderTexture("WaterRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					reflectionCamera.targetTexture = renderTexture;
//					if (textureCamera != null) textureCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.OceanEffect:
//					SetupRenderTexture("OceanRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, renderTexture, noiseTexture);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					reflectionCamera.targetTexture = renderTexture;
//					if (textureCamera != null) textureCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				default:
//					var defaultData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//					defaultData.cameraStack.Clear();
//					defaultData.cameraStack.Add(reflectionCamera);
//					outputStage = mainCamera;
//					break;
//			}

//			reflectionCamera.targetTexture = renderTexture;

//			if (textureCamera != null &&
//				(effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect ||
//				 effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect))
//			{
//				textureCamera.targetTexture = renderTexture;
//			}

//			var mainCameraData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//			if (postProcessingCamera != null) mainCameraData.cameraStack.Add(postProcessingCamera);

//			UpdateMaterialProperties();
//			SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);

//			if (outputStage != null)
//			{
//				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
//				if (provider == null)
//					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

//				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
//				(cmd, cam) =>
//				{
//					if (effectMesh == null) return;
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
//				   activeSkybox != lastSkyboxMaterial;
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
//			lastSkyboxMaterial = activeSkybox;
//		}

//		private void UpdateMaterialProperties()
//		{
//			if (effectMaterial == null) return;

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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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

//		public void ApplyWaterDefault()
//		{
//			baseColor = new Color(0, 0, 0, 0.5f);
//			rippleSpeed = 0.25f;
//			rippleAmplitude = 0.25f;
//			rippleFrequency = 0.35f;
//			reflectionStrength = 0.5f;
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
//				previousEffectMode = effectMode;
//			}
//			else if (effectMaterial != null)
//			{
//				UpdateMaterialProperties();
//			}
//		}

//		public void ForceSkyboxUpdate()
//		{
//			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//			{
//				lastSkyboxMaterial = activeSkybox;
//				SkyboxUtility.SetSkyboxCubemap(effectMaterial, lastSkyboxMaterial);
//			}
//		}

//		private void CleanupDynamicResources()
//		{
//			RenderTexture.active = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

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
//			if (renderTexture != null)
//			{
//				DestroyImmediate(renderTexture);
//				renderTexture = null;
//			}
//			// Do NOT destroy textureCamera here — it's now created in Awake for water modes
//		}

//		private void CleanupOrphanedChildren()
//		{
//			for (int i = transform.childCount - 1; i >= 0; i--)
//			{
//				var child = transform.GetChild(i);
//				if (child.name == "ReflectionCamera" || child.name == "TextureCamera")
//				{
//					if (child.GetComponent<Camera>() != null)
//					{
//						DestroyImmediate(child.gameObject);
//					}
//				}
//			}
//		}

//		void OnDestroy()
//		{
//			RenderTexture.active = null;
//			if (mainCamera != null) mainCamera.targetTexture = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

//			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
//			if (effectMesh != null) DestroyImmediate(effectMesh);
//			if (renderTexture != null) DestroyImmediate(renderTexture);
//			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);

//			CleanupOrphanedChildren();
//		}
//	}
//}

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
//		public void SetEffectMode(EffectMode value)
//		{
//			effectMode = value;
//			if (value == EffectMode.Water)
//				ApplyWaterDefault();
//		}

//		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection)")]
//		private Color mirrorTint = new Color(1f, 1f, 1f, 1f);

//		[SerializeField, Tooltip("Base color for Frost, Water, Ocean")]
//		private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);

//		[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
//		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
//		[SerializeField] private Texture2D noiseTexture;

//		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

//		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;
//		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;

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

//		//temporary workaround
//		[Header("Preview Mode (internal)")]
//		[SerializeField, HideInInspector] private bool isPreviewCamera = false;
//		public void SetPreviewMode(bool value) => isPreviewCamera = value;

//		private Material activeSkybox => overrideSkyboxMaterial != null ? overrideSkyboxMaterial : RenderSettings.skybox;

//		void Awake()
//		{
//			mainCamera = GetComponent<Camera>();
//			if (mainCamera == null)
//			{
//				Debug.LogError("Camera component missing.", this);
//				enabled = false;
//				return;
//			}
//			if (Camera.main == mainCamera)
//				PreviewRenderLayers.RemovePreviewLayers(mainCamera);

//			CleanupOrphanedChildren();

//			if (reflectionCamera == null)
//			{
//				var obj = new GameObject("ReflectionCamera");
//				obj.transform.SetParent(transform, false);
//				reflectionCamera = obj.AddComponent<Camera>();
//				reflectionCamera.clearFlags = CameraClearFlags.Nothing;
//				reflectionCamera.cullingMask = mainCamera.cullingMask;
//				reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

//				var provider = obj.AddComponent<CameraCommandProvider>();
//				provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
//				provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.renderType = CameraRenderType.Overlay;
//				URPCameraHelper.SetClearDepth(data, false);
//			}
//		}

//		void Start()
//		{
//			InitializeEffect();
//			previousEffectMode = effectMode;
//			StoreMaterialPropertyValues();
//		}

//		private Material overrideSkyboxMaterial;
//		public void SetSkyboxOverride(Material value)
//		{
//			//lastSkyboxMaterial = activeSkybox;
//			overrideSkyboxMaterial = value;

//			//if (effectMaterial == null) return;
//			//SkyboxUtility.SetSkyboxCubemap(effectMaterial, overrideSkyboxMaterial);//lastSkyboxMaterial
//		}

//		private void InitializeEffect()
//		{
//			CleanupDynamicResources();

//			if (effectMode == EffectMode.SurfaceFilm || effectMode == EffectMode.FrostEffect || effectMode == EffectMode.OceanEffect)
//			{
//				if (noiseTexture == null)
//				{
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
//					SetupRenderTexture("RenderTexture");
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
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
//					isMaterialDynamic = true;
//					SetupTextureCamera();
//					reflectionCamera.targetTexture = renderTexture;
//					outputStage = mainCamera;
//					break;

//				case EffectMode.OceanEffect:
//					SetupRenderTexture("OceanRenderTexture");
//					effectMesh = new Mesh();
//					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, renderTexture, noiseTexture);
//					SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
//			if (postProcessingCamera != null) mainCameraData.cameraStack.Add(postProcessingCamera);

//			UpdateMaterialProperties();
//			SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);

//			if (outputStage != null)
//			{
//				var provider = outputStage.gameObject.GetComponent<CameraCommandProvider>();
//				if (provider == null)
//					provider = outputStage.gameObject.AddComponent<CameraCommandProvider>();

//				provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents,
//				(cmd, cam) =>
//				{
//					if (effectMesh == null) return;
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
//				textureCamera.cullingMask &= ~(1 << PreviewRenderLayers.previewTransparentLayer);
//				textureCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
//				textureCamera.targetTexture = renderTexture;
//				textureCamera.depth = mainCamera.depth - 1;
//				var data = obj.AddComponent<UniversalAdditionalCameraData>();
//				data.cameraStack.Clear();
//				data.cameraStack.Add(reflectionCamera);
//				obj.AddComponent<CameraCommandProvider>();

//				if (isPreviewCamera)
//				{
//					var ambientOverride = textureCamera.gameObject.AddComponent<ClassicTilestorm.PreviewAmbientOverride>();
//					Debug.Log($"PreviewAmbientOverride attached to TextureCamera (preview only) - ToDo: get rid of this hack!!!");
//				}
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
//				   activeSkybox != lastSkyboxMaterial;
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
//			lastSkyboxMaterial = activeSkybox;
//		}

//		private void UpdateMaterialProperties()
//		{
//			if (effectMaterial == null) return;

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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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
//						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);
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

//		public void ApplyWaterDefault()
//		{
//			baseColor = new Color(0, 0, 0, 0.5f);
//			rippleSpeed = 0.25f;
//			rippleAmplitude = 0.25f;
//			rippleFrequency = 0.35f;
//			reflectionStrength = 0.5f;
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
//				previousEffectMode = effectMode;
//			}
//			else if (effectMaterial != null)
//			{
//				UpdateMaterialProperties();
//			}
//		}

//		public void ForceSkyboxUpdate()
//		{
//			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
//			{
//				lastSkyboxMaterial = activeSkybox;
//				SkyboxUtility.SetSkyboxCubemap(effectMaterial, lastSkyboxMaterial);
//			}
//		}

//		private void CleanupDynamicResources()
//		{
//			RenderTexture.active = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

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
//			if (renderTexture != null)
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

//		private void CleanupOrphanedChildren()
//		{
//			for (int i = transform.childCount - 1; i >= 0; i--)
//			{
//				var child = transform.GetChild(i);
//				if (child.name == "ReflectionCamera" || child.name == "TextureCamera")
//				{
//					if (child.GetComponent<Camera>() != null)
//					{
//						DestroyImmediate(child.gameObject);
//					}
//				}
//			}
//		}

//		void OnDestroy()
//		{
//			RenderTexture.active = null;
//			if (mainCamera != null) mainCamera.targetTexture = null;
//			if (reflectionCamera != null) reflectionCamera.targetTexture = null;
//			if (textureCamera != null) textureCamera.targetTexture = null;

//			if (effectMaterial != null && isMaterialDynamic) DestroyImmediate(effectMaterial);
//			if (effectMesh != null) DestroyImmediate(effectMesh);
//			if (renderTexture != null) DestroyImmediate(renderTexture);
//			if (isTextureDynamic && noiseTexture != null) DestroyImmediate(noiseTexture);

//			CleanupOrphanedChildren();
//		}
//	}
//}
