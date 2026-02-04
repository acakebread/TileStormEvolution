using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	[System.Serializable]
	public struct PerfectMirrorDefaults
	{
		public Color tint;

		public static PerfectMirrorDefaults Get() => new()
		{
			tint = new Color(0.5f, 0.5f, 0.5f, 1f),
		};
	}

	[System.Serializable]
	public struct SurfaceFilmDefaults
	{
		public Color tint;
		public float noiseScale;

		public static SurfaceFilmDefaults Get() => new()
		{
			tint = new Color(0.5f, 0.5f, 0.5f, 0.5f),
			noiseScale = 1f,
		};
	}

	[System.Serializable]
	public struct FrostDefaults
	{
		public Color tint;
		public float depth;
		public float noiseStrength;

		public static FrostDefaults Get() => new()
		{
			tint = new Color(1f, 1f, 1f, 0.1f),
			depth = 0.05f,
			noiseStrength = 0.2f,
		};
	}

	[System.Serializable]
	public struct WaterDefaults
	{
		public Color tint;
		public float rippleSpeed;
		public float rippleAmplitude;
		public float rippleFrequency;
		public float rippleOffset;
		public float reflectionStrength;

		public static WaterDefaults Get() => new()
		{
			tint = new Color(0.05f, 0.05f, 0.05f, 0.6f),
			rippleSpeed = 0.2f,
			rippleAmplitude = 0.075f,
			rippleFrequency = 0.35f,
			rippleOffset = 0f,
			reflectionStrength = 0.8f,
		};
	}

	[System.Serializable]
	public struct OceanDefaults
	{
		public Color tint;
		public float rippleSpeed;
		public float rippleAmplitude;
		public float rippleFrequency;
		public float rippleOffset;
		public float frostDepth;
		public float noiseStrength;
		public float frostThreshold;
		public float frostFadeRange;

		public static OceanDefaults Get() => new()
		{
			tint = new Color(0.5f, 0.5f, 0.5f, 0.5f),
			rippleSpeed = 0.05f,
			rippleAmplitude = 0.05f,
			rippleFrequency = 0.015f,
			rippleOffset = 0f,
			frostDepth = 0.15f,
			noiseStrength = 0.15f,
			frostThreshold = 0.65f,
			frostFadeRange = 0.065f,
		};
	}

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

		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection) and Base color for Frost, Water, Ocean")]
		private Color mirrorTint = new (0.1f, 0.1f, 0.1f, 0.5f);

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

		private CameraRenderSettingsOverride renderSettingsOverride => gameObject.GetComponent<CameraRenderSettingsOverride>();
		private Texture skyboxTexture => SkyboxUtility.GetSkyboxTexture(renderSettingsOverride ? renderSettingsOverride.OverrideSettings.skybox : RenderSettings.skybox);

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

			lastAppliedMode = effectMode;//for invalidate to work at run time

			// Intelligent initialization logic
			if (effectMode == EffectMode.Null)
			{
				// Wait one frame to see if something sets it (e.g. script, editor)
				Invoke(nameof(CheckAndApplyDefaultEffectAfterDelay), 0f); // 0f = next frame
			}
			else
			{
				// Already set in inspector/script → apply immediately
				ApplyEffect(effectMode, false);
			}
		}

		private void CheckAndApplyDefaultEffectAfterDelay()
		{
			if (effectMode == EffectMode.Null)
			{
				// Still null after one frame → apply default
				effectMode = EffectMode.Debug; // or PerfectMirror, Water, whatever you prefer as fallback
				ApplyEffect(effectMode, false);
				Debug.Log($"Auto-applied default effect {effectMode} after one-frame delay (was Null)", this);
			}
			else
			{
				// Something set it during that frame → apply now
				ApplyEffect(effectMode, false);
				Debug.Log($"Effect was set to {effectMode} during delay frame — applying immediately", this);
			}
		}

		public void SetEffectMode(string value, bool useDefaults = true) => SetEffectMode(ParseEffectMode(value), useDefaults);

		public void SetEffectMode(EffectMode value, bool useDefaults = true)
		{
			if (effectMode == value) return;

			effectMode = value;
			if (useDefaults) ApplyDefaults(value);
			ApplyEffect(value);
		}

		public EffectMode CurrentEffectMode => effectMode;

		public void ApplyDefaults(EffectMode value)
		{
			switch (value)
			{
				case EffectMode.PerfectMirror:
					var pm = PerfectMirrorDefaults.Get();
					mirrorTint = pm.tint;
					break;

				case EffectMode.SurfaceFilm:
					var sf = SurfaceFilmDefaults.Get();
					mirrorTint = sf.tint;
					noiseScale = sf.noiseScale;
					break;

				case EffectMode.FrostEffect:
					var fr = FrostDefaults.Get();
					mirrorTint = fr.tint;
					frostDepth = fr.depth;
					noiseStrength = fr.noiseStrength;
					break;

				case EffectMode.Water:
					var w = WaterDefaults.Get();
					mirrorTint = w.tint;
					rippleSpeed = w.rippleSpeed;
					rippleAmplitude = w.rippleAmplitude;
					rippleFrequency = w.rippleFrequency;
					rippleOffset = w.rippleOffset;
					reflectionStrength = w.reflectionStrength;
					break;

				case EffectMode.OceanEffect:
					var o = OceanDefaults.Get();
					mirrorTint = o.tint;
					rippleSpeed = o.rippleSpeed;
					rippleAmplitude = o.rippleAmplitude;
					rippleFrequency = o.rippleFrequency;
					rippleOffset = o.rippleOffset;
					frostDepth = o.frostDepth;
					noiseStrength = o.noiseStrength;
					frostThreshold = o.frostThreshold;
					frostFadeRange = o.frostFadeRange;
					break;

				// Debug, Null → no defaults applied
			}
		}

		private void ApplyEffect(EffectMode value, bool withReset = true)
		{
			effectMode = value;

			if (withReset)
				CleanupDynamicResources();// cleans both textures + cameras etc.

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
					effectMaterial = MaterialUtils.CreatePerlinWangOpaque(renderTexture, mirrorTint, noiseTexture, mirrorTint.a, noiseScale);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					break;

				case EffectMode.FrostEffect:
					renderTexture.name = "RenderTexture";
					effectMesh = new Mesh();
					noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
					isTextureDynamic = true;
					effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(mirrorTint, frostDepth, renderTexture, noiseTexture, noiseStrength);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					break;

				case EffectMode.Water:
					renderTexture.name = "WaterRenderTexture";
					effectMesh = new Mesh();
					effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(mirrorTint, renderTexture, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, reflectionStrength, skyboxTexture);
					isMaterialDynamic = true;
					SetupTextureCamera();
					reflectionCamera.targetTexture = renderTexture;
					break;

				case EffectMode.OceanEffect:
					renderTexture.name = "OceanRenderTexture";
					effectMesh = new Mesh();
					noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
					isTextureDynamic = true;
					effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(mirrorTint, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset, frostDepth, noiseStrength, frostThreshold, frostFadeRange, null, noiseTexture);
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

		public void UpdateMaterialProperties()
		{
			if (effectMaterial == null) return;

			// Lazy one-time assignment of reflection RT to material (after RT is created & camera is rendering)
			if (renderTexture != null && effectMaterial.HasProperty("_MainTex") && effectMaterial.GetTexture("_MainTex") == null)
				effectMaterial.SetTexture("_MainTex", renderTexture);

			switch (effectMode)
			{
				case EffectMode.PerfectMirror:
					effectMaterial.SetColor("_DimColor", mirrorTint);
					break;
				case EffectMode.SurfaceFilm:
					effectMaterial.SetColor("_DimColor", mirrorTint);
					effectMaterial.SetFloat("_FilmIntensity", mirrorTint.a);
					effectMaterial.SetFloat("_NoiseScale", noiseScale);
					effectMaterial.SetTexture("_NoiseTex", noiseTexture);
					break;

				case EffectMode.FrostEffect:
					effectMaterial.SetColor("_BaseColor", mirrorTint);
					effectMaterial.SetFloat("_Depth", frostDepth);
					effectMaterial.SetFloat("_NoiseStrength", noiseStrength);
					effectMaterial.SetTexture("_NoiseTex", noiseTexture);
					break;

				case EffectMode.Water:
					effectMaterial.SetColor("_BaseColor", mirrorTint);
					effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
					effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
					effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
					effectMaterial.SetFloat("_RippleOffset", rippleOffset);
					effectMaterial.SetFloat("_ReflectionStrength", reflectionStrength);
					effectMaterial.SetTexture("_Skybox", skyboxTexture);
					break;

				case EffectMode.OceanEffect:
					effectMaterial.SetColor("_BaseColor", mirrorTint);
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
					effectMaterial.SetTexture("_Skybox", skyboxTexture);
					break;
			}
		}

		public void Update()
		{
			timeSeed += Time.deltaTime;

			if (effectMode == EffectMode.Water || effectMode == EffectMode.OceanEffect)
				effectMaterial.SetFloat("_TimeSeed", timeSeed);
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
			if (!isActiveAndEnabled)
				return;

			// Safe: live update material properties (colors, floats, textures)
			if (Application.isPlaying && lastAppliedMode == effectMode) UpdateMaterialProperties();
			else ApplyDefaults(effectMode);

			if (mainCamera == null)
				return;//only update properties in editor mode

			// If mode changed (or first time), schedule rebuild
			if (effectMode != lastAppliedMode || lastAppliedMode == EffectMode.Null)
				Invoke(nameof(DeferredRebuild), 0f);  // next frame / safe timing
			lastAppliedMode = effectMode;  // track for next change detection
		}

		private void DeferredRebuild() => ApplyEffect(effectMode, true);

		public void UpdateRenderSettings(UnityRenderSettings renderSettings)
		{
			foreach (var childCam in GetComponentsInChildren<Camera>(true))
			{
				var overrideComp = childCam.gameObject.GetComponent<CameraRenderSettingsOverride>();
				if (null == overrideComp)
					overrideComp = childCam.gameObject.AddComponent<CameraRenderSettingsOverride>();
				overrideComp.OverrideSettings = renderSettings;
			}
			UpdateMaterialProperties();
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

			// Rebuild everything with new mode context
			ApplyEffect(effectMode);
		}

		// Public accessor for UI / preview system
		public RenderTexture GetOutputTexture() => isRenderToTextureMode ? outputRenderTexture : null;

		private void SafeReleaseAndNull(ref RenderTexture rt)
		{
			if (rt == null) return;

			// 1. Detach from GPU / pipeline
			if (rt.IsCreated())
				rt.Release();

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

		// Returns the enum value that best matches the stored string
		// Returns EffectMode.Null if no match or input is null/empty
		public static EffectMode ParseEffectMode(string effectString)
		{
			if (string.IsNullOrWhiteSpace(effectString))
				return EffectMode.Null;

			// Try exact match first (case-insensitive)
			if (Enum.TryParse<EffectMode>(effectString, ignoreCase: true, out var mode))
			{
				// We usually don't want to restore "Null" from string
				return mode == EffectMode.Null ? EffectMode.Debug : mode;
			}

			// Optional: fallback / alias mapping (very useful in real projects)
			return effectString.ToLowerInvariant() switch
			{
				"debug" => EffectMode.Debug,
				"mirror" => EffectMode.PerfectMirror,
				"film" => EffectMode.SurfaceFilm,
				"frost" => EffectMode.FrostEffect,
				"water" => EffectMode.Water,
				"ocean" => EffectMode.OceanEffect,
				"perfectmirror" => EffectMode.PerfectMirror,
				"surfacefilm" => EffectMode.SurfaceFilm,
				"watereffect" => EffectMode.Water,
				"oceaneffect" => EffectMode.OceanEffect,
				_ => EffectMode.Null   // or EffectMode.Debug as fallback
			};
		}

		// Converts enum back to the string you want to store / show in UI
		public static string EffectModeToString(EffectMode mode)
		{
			return mode switch
			{
				EffectMode.Null => null,
				EffectMode.Debug => "Debug",
				EffectMode.PerfectMirror => "Mirror",
				EffectMode.SurfaceFilm => "Film",
				EffectMode.FrostEffect => "Frost",
				EffectMode.Water => "Water",
				EffectMode.OceanEffect => "Ocean",
				_ => null
			};
		}
	}
}
