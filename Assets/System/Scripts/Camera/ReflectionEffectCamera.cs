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
			Null,// "not yet initialized"
			Debug,
			PerfectMirror,
			SurfaceFilm,
			FrostEffect,
			Water,
			OceanEffect
		}

		[SerializeField] private EffectMode effectMode = EffectMode.Null;

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

		private CameraRenderSettingsOverride renderSettingsOverride => gameObject.GetComponent<CameraRenderSettingsOverride>();
		private Material activeSkybox => renderSettingsOverride ? renderSettingsOverride.OverrideSettings.skybox : RenderSettings.skybox;

		// Already there
		private bool isRenderToTextureMode = false;
		private RenderTexture outputRenderTexture;

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

			// Setup camera data and command provider (always needed)
			var mainCameraData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
			if (postProcessingCamera != null) mainCameraData.cameraStack.Add(postProcessingCamera);

			var provider = mainCamera.gameObject.GetComponent<CameraCommandProvider>();
			if (provider == null)
				provider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();

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

			// Intelligent initialization logic
			if (effectMode == EffectMode.Null)
			{
				// Wait one frame to see if something sets it (e.g. script, editor)
				Invoke(nameof(CheckAndApplyDefaultEffectAfterDelay), 0f); // 0f = next frame
			}
			else
			{
				// Already set in inspector/script → apply immediately
				ApplyEffect(effectMode);
			}
		}

		private void CheckAndApplyDefaultEffectAfterDelay()
		{
			if (effectMode == EffectMode.Null)
			{
				// Still null after one frame → apply default
				effectMode = EffectMode.Debug; // or PerfectMirror, Water, whatever you prefer as fallback
				ApplyEffect(effectMode);
				Debug.Log($"Auto-applied default effect {effectMode} after one-frame delay (was Null)", this);
			}
			else
			{
				// Something set it during that frame → apply now
				ApplyEffect(effectMode);
				Debug.Log($"Effect was set to {effectMode} during delay frame — applying immediately", this);
			}
		}

		public void SetEffectMode(EffectMode value)
		{
			if (value == EffectMode.Water)
				ApplyWaterDefault();

			CleanupDynamicResources();

			ApplyEffect(value);
		}

		private void ApplyEffect(EffectMode value)
		{
			effectMode = value;

			CreateOrResizeReflectionTexture(Screen.width, Screen.height);

			var obj = new GameObject("ReflectionCamera");
			obj.transform.SetParent(transform, false);
			reflectionCamera = obj.AddComponent<Camera>();
			reflectionCamera.clearFlags = CameraClearFlags.Nothing;
			reflectionCamera.cullingMask = mainCamera.cullingMask;
			reflectionCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));
			reflectionCamera.targetTexture = renderTexture;

			var _provider = obj.AddComponent<CameraCommandProvider>();
			_provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
			_provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Overlay;
			URPCameraHelper.SetClearDepth(data, false);

			switch (effectMode)
			{
				case EffectMode.PerfectMirror:
					renderTexture.name = "RenderTexture";
					effectMesh = new Mesh();
					effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(renderTexture, mirrorTint);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					break;

				case EffectMode.SurfaceFilm:
					renderTexture.name = "MirrorWithFilmRT";
					effectMesh = new Mesh();
					noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
					isTextureDynamic = true;
					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, filmIntensity, noiseScale);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					break;

				case EffectMode.FrostEffect:
					renderTexture.name = "RenderTexture";
					effectMesh = new Mesh();
					noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
					isTextureDynamic = true;
					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(baseColor, frostDepth, renderTexture, noiseTexture, noiseStrength);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					break;

				case EffectMode.Water:
					renderTexture.name = "WaterRenderTexture";
					effectMesh = new Mesh();
					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(baseColor, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					break;

				case EffectMode.OceanEffect:
					renderTexture.name = "OceanRenderTexture";
					effectMesh = new Mesh();
					noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
					isTextureDynamic = true;
					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(baseColor, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, null, noiseTexture);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					break;

				default:
					var defaultData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
					defaultData.cameraStack.Clear();
					defaultData.cameraStack.Add(reflectionCamera);
					break;
			}

			UpdateMaterialProperties();
			StoreMaterialPropertyValues();

			void SetupTextureCamera()
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
					effectMaterial.SetTexture("_MainTex", renderTexture);

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
						//effectMaterial.SetTexture("_Skybox", activeSkybox.mainTexture);//this doesn't work
						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);//this does
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
						//effectMaterial.SetTexture("_Skybox", activeSkybox.mainTexture);//this doesn't work
						SkyboxUtility.SetSkyboxCubemap(effectMaterial, activeSkybox);//this does
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
			timeSeed += Time.deltaTime;
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

			// Safe: live update material properties (colors, floats, textures)
			if (effectMaterial != null)
				UpdateMaterialProperties();

			// If mode changed (or first time), schedule rebuild
			if (effectMode != lastAppliedMode || lastAppliedMode == EffectMode.Null)
				Invoke(nameof(DeferredRebuild), 0f);  // next frame / safe timing

			lastAppliedMode = effectMode;  // track for next change detection
		}

		private void DeferredRebuild()
		{
			CleanupDynamicResources();
			ApplyEffect(effectMode);
		}

		public void UpdateRenderSettings(UnityRenderSettings renderSettings)
		{
			foreach (var childCam in GetComponentsInChildren<Camera>(true))
			{
				var overrideComp = childCam.gameObject.GetComponent<CameraRenderSettingsOverride>();
				if (null == overrideComp)
					overrideComp = childCam.gameObject.AddComponent<CameraRenderSettingsOverride>();
				overrideComp.OverrideSettings = renderSettings;
			}
			lastSkyboxMaterial = null;//temporary hack
		}

		private EffectMode lastAppliedMode = EffectMode.Null;  // track last successfully applied

		public void SetExternalOutputTexture(RenderTexture externalRT)
		{
			if (isRenderToTextureMode)
			{
				Debug.LogWarning("SetExternalOutputTexture has no effect when isRenderToTextureMode is true.");
				return;
			}

			if (mainCamera == null) return;

			// Clean up previous internal one if any (shouldn't exist, but safety)
			SafeReleaseAndNull(ref outputRenderTexture);

			mainCamera.targetTexture = externalRT;
		}

		// Public setter (call before SetEffectMode ideally)
		public void SetRenderToTextureMode(bool enable)
		{
			if (isRenderToTextureMode == enable) return;
			isRenderToTextureMode = enable;

			CleanupDynamicResources();           // cleans both textures + cameras etc.

			// Rebuild everything with new mode context
			ApplyEffect(effectMode);
		}

		// Public accessor for UI / preview system
		public RenderTexture GetOutputTexture()
		{
			return isRenderToTextureMode ? outputRenderTexture : null;
		}

		private void SafeReleaseAndNull(ref RenderTexture rt)
		{
			if (rt == null) return;

			// 1. Detach from GPU / pipeline
			if (rt.IsCreated())
			{
				rt.Release();
			}

			// 2. Null out any known camera references (defensive)
			if (mainCamera != null && mainCamera.targetTexture == rt)
				mainCamera.targetTexture = null;
			if (reflectionCamera != null && reflectionCamera.targetTexture == rt)
				reflectionCamera.targetTexture = null;
			if (textureCamera != null && textureCamera.targetTexture == rt)
				textureCamera.targetTexture = null;

			// 3. Destroy (safe now that it's released and detached)
			DestroyImmediate(rt);

			rt = null;
		}

		// The single resize function — now handles both textures when in mode
		public void CreateOrResizeReflectionTexture(int targetWidth, int targetHeight)
		{
			targetWidth = Mathf.Max(16, targetWidth);
			targetHeight = Mathf.Max(16, targetHeight);

			// ── Reflection texture (always exists) ─────────────────────────────────
			bool reflectionNeedsResize = renderTexture == null ||
										 renderTexture.width != targetWidth ||
										 renderTexture.height != targetHeight;

			if (reflectionNeedsResize)
			{
				RenderTexture.active = null;
				if (reflectionCamera) reflectionCamera.targetTexture = null;
				if (textureCamera) textureCamera.targetTexture = null;

				if (renderTexture != null)
				{
					DestroyImmediate(renderTexture);
					renderTexture = null;
				}

				renderTexture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
				{
					name = $"Reflection_{effectMode}",
					useMipMap = false,
					autoGenerateMips = false,
					filterMode = FilterMode.Bilinear,
					useDynamicScale = true
				};
				renderTexture.Create();

				if (reflectionCamera) reflectionCamera.targetTexture = renderTexture;
				if (textureCamera) textureCamera.targetTexture = renderTexture;

				// Update material if already exists
				if (effectMaterial != null && effectMaterial.HasProperty("_MainTex"))
					effectMaterial.SetTexture("_MainTex", renderTexture);
			}

			// ── Output texture (only in render-to-texture mode) ─────────────────────
			if (isRenderToTextureMode)
			{
				bool outputNeedsResize = outputRenderTexture == null ||
										 outputRenderTexture.width != targetWidth ||
										 outputRenderTexture.height != targetHeight;

				if (outputNeedsResize)
				{
					if (mainCamera) mainCamera.targetTexture = null;

					if (outputRenderTexture != null)
					{
						DestroyImmediate(outputRenderTexture);
						outputRenderTexture = null;
					}

					outputRenderTexture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
					{
						name = $"Output_{effectMode}",
						filterMode = FilterMode.Bilinear,
						useDynamicScale = true
					};
					outputRenderTexture.Create();

					if (mainCamera) mainCamera.targetTexture = outputRenderTexture;
				}
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
				isTextureDynamic = false;
			}
			if (renderTexture != null)
			{
				DestroyImmediate(renderTexture);
				renderTexture = null;
			}
			if (null != reflectionCamera)
			{
				DestroyImmediate(reflectionCamera.gameObject);
				reflectionCamera = null;
			}
			if (null != textureCamera)
			{
				DestroyImmediate(textureCamera.gameObject);
				textureCamera = null;
			}
			SafeReleaseAndNull(ref outputRenderTexture);
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
			if (null != reflectionCamera) DestroyImmediate(reflectionCamera.gameObject);
			if (null != textureCamera) DestroyImmediate(textureCamera.gameObject);
		}
	}
}
