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
			Null,
			Debug,
			PerfectMirror,
			SurfaceFilm,
			FrostEffect,
			Water,
			//OceanEffect
		}

		[SerializeField] private EffectMode effectMode = EffectMode.Null;

		[SerializeField, Tooltip("Tint for PerfectMirror and SurfaceFilm (multiplies reflection) and Base color for Frost, Water, Ocean")]
		private Color mirrorTint = new Color(0.1f, 0.1f, 0.1f, 0.5f);

		[SerializeField, Range(0.01f, 5f)] private float noiseScale = 1f;
		[SerializeField] private Texture2D noiseTexture;

		[SerializeField, Range(0f, 1f)] private float frostDepth = 0.5f;
		[SerializeField, Range(0f, 1f)] private float noiseStrength = 0.5f;

		[SerializeField, Range(0f, 1f)] private float rippleSpeed = 0.5f;
		[SerializeField, Range(0f, 1f)] private float rippleAmplitude = 0.5f;
		[SerializeField, Range(0f, 1f)] private float rippleFrequency = 0.5f;
		[SerializeField, Range(0f, 1f)] private float rippleOffset = 0.5f;

		[SerializeField, Range(0f, 1f)] private float reflectionStrength = 0.5f;
		[SerializeField, Range(1f, 40f), Tooltip("Higher values = reflection appears only at very grazing angles\nLower values = broader reflection")]
		private float fresnelSharpness = 12f;

		//[SerializeField, Range(0f, 1f)] private float frostThreshold = 0.8f;//not currently used
		//[SerializeField, Range(0f, 0.2f)] private float frostFadeRange = 0.1f;

		private Camera mainCamera;
		private Camera reflectionCamera;
		private Camera textureCamera;
		private Camera postProcessingCamera;
		private RenderTexture effectRT;
		private RenderTexture outputRT;

		private Mesh effectMesh;
		private Material effectMaterial;
		private bool isTextureDynamic;
		private float timeSeed;

		private Texture tintedSkyboxTexture;

		private void Awake()
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

			var ppCamera = transform.GetComponentInChildren<PostProcessingCameraController>(true);
			if (ppCamera) postProcessingCamera = ppCamera.GetComponent<Camera>();

			var provider = mainCamera.gameObject.GetOrAddComponent<CameraCommandProvider>();
			provider.RegisterCommand(RenderPassEvent.BeforeRenderingTransparents, OnBeforeRenderingTransparents);

			var mainCameraData = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
			if (postProcessingCamera) mainCameraData.cameraStack.Add(postProcessingCamera);

			CreateReflectionCamera();
			CreateTextureCamera();

			effectMesh = new Mesh();

			// Force initial build
			EffectMode startMode = effectMode;
			effectMode = EffectMode.Null;   // Force rebuild on first call

			if (startMode == EffectMode.Null)
				Invoke(nameof(CheckAndApplyDefaultEffectAfterDelay), 0f);
			else
				UpdateEffect(startMode);

			UpdateEffectRT(Screen.width, Screen.height);
		}

		private void OnBeforeRenderingTransparents(RasterCommandBuffer cmd, Camera cam)
		{
			if (effectMesh == null || effectMaterial == null) return;
			FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh, true);
			if (effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3)
			{
				effectMaterial.SetPass(0);
				cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
			}
		}

		private void CheckAndApplyDefaultEffectAfterDelay()
		{
			tintedSkyboxTexture = CubemapUtility.GetTintedCubemapInstance(RenderSettings.skybox);

			var modeToApply = effectMode != EffectMode.Null ? effectMode : EffectMode.Debug;
			if (effectMode == EffectMode.Null)
				Debug.Log($"Auto-applied default effect {modeToApply} after one-frame delay (was Null)", this);

			UpdateEffect(modeToApply);
		}

		private void CreateReflectionCamera()
		{
			var obj = new GameObject("ReflectionCamera");
			obj.transform.SetParent(transform, false);
			reflectionCamera = obj.AddComponent<Camera>();
			reflectionCamera.clearFlags = CameraClearFlags.Nothing;
			reflectionCamera.cullingMask = mainCamera.cullingMask & ~(1 << LayerMask.NameToLayer("Editor"));

			var _provider = obj.AddComponent<CameraCommandProvider>();
			_provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => cmd.SetInvertCulling(true));
			_provider.RegisterCommand(RenderPassEvent.AfterRenderingOpaques, (cmd, cam) => cmd.SetInvertCulling(false));

			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Overlay;
			URPCameraHelper.SetClearDepth(data, false);
		}

		private void CreateTextureCamera()
		{
			var obj = new GameObject("TextureCamera");
			obj.transform.SetParent(transform, false);
			textureCamera = obj.AddComponent<Camera>();
			textureCamera.CopyFrom(mainCamera);
			textureCamera.clearFlags = mainCamera.clearFlags;
			textureCamera.cullingMask = mainCamera.cullingMask
				& ~(1 << PreviewRenderLayers.previewTransparentLayer)
				& ~(1 << LayerMask.NameToLayer("TransparentFX"))
				& ~(1 << LayerMask.NameToLayer("Editor"));
			textureCamera.depth = mainCamera.depth - 1;

			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.cameraStack.Clear();
			data.cameraStack.Add(reflectionCamera);
			obj.AddComponent<CameraCommandProvider>();
		}

		public void OnEffectChanged(EffectMode value) => SetEffectMode(value);

		public void SetEffectMode(string value, bool useDefaults = true) => SetEffectMode(ParseEffectMode(value), useDefaults);

		public void SetEffectMode(EffectMode value, bool useDefaults = true)
		{
			if (useDefaults) ApplyDefaults(value);
			UpdateEffect(value);
		}

		public void ApplyDefaults(EffectMode value)
		{
			switch (value)
			{
				case EffectMode.PerfectMirror:
					mirrorTint = MaterialUtils.PerfectMirrorDefaults.Get().tint;
					break;
				case EffectMode.SurfaceFilm:
					var sf = MaterialUtils.SurfaceFilmDefaults.Get();
					mirrorTint = sf.tint;
					noiseScale = sf.noiseScale;
					reflectionStrength = 0.35f;   // good default for film
					fresnelSharpness = 15f;
					break;
				case EffectMode.FrostEffect:
					var fr = MaterialUtils.FrostDefaults.Get();
					mirrorTint = fr.tint;
					frostDepth = fr.depth;
					noiseStrength = fr.noiseStrength;
					reflectionStrength = 0.3f;    // good default for frost
					fresnelSharpness = 18f;
					break;
				case EffectMode.Water:
					var w = MaterialUtils.WaterDefaults.Get();
					mirrorTint = w.tint;
					rippleSpeed = w.rippleSpeed;
					rippleAmplitude = w.rippleAmplitude;
					rippleFrequency = w.rippleFrequency;
					rippleOffset = w.rippleOffset;
					reflectionStrength = w.reflectionStrength;
					fresnelSharpness = 12f;
					break;
				//case EffectMode.OceanEffect:
				//	var o = MaterialUtils.OceanDefaults.Get();
				//	mirrorTint = o.tint;
				//	rippleSpeed = o.rippleSpeed;
				//	rippleAmplitude = o.rippleAmplitude;
				//	rippleFrequency = o.rippleFrequency;
				//	rippleOffset = o.rippleOffset;
				//	frostDepth = o.frostDepth;
				//	noiseStrength = o.noiseStrength;
				//	frostThreshold = o.frostThreshold;
				//	frostFadeRange = o.frostFadeRange;
				//	reflectionStrength = 0.4f;
				//	fresnelSharpness = 14f;
				//	break;
			}
		}

		private void UpdateEffect(EffectMode value)
		{
			if (value == EffectMode.Null)
				value = EffectMode.Debug;

			bool modeChanged = effectMode != value || effectMaterial == null;

			if (modeChanged)
			{
				Debug.Log($"[ReflectionEffectCamera] Rebuilding material for mode: {value}", this);

				// Cleanup previous material and dynamic texture
				if (effectMaterial)
				{
					DestroyImmediate(effectMaterial);
					effectMaterial = null;
				}
				if (noiseTexture && isTextureDynamic)
				{
					DestroyImmediate(noiseTexture);
					noiseTexture = null;
					isTextureDynamic = false;
				}

				// Create new material
				switch (value)
				{
					case EffectMode.PerfectMirror:
						effectMaterial = MaterialUtils.CreatePerfectMirrorOpaqueMaterial(effectRT, mirrorTint);
						break;

					case EffectMode.SurfaceFilm:
						noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
						isTextureDynamic = true;
						effectMaterial = MaterialUtils.CreatePerlinWangOpaque(
							effectRT, mirrorTint, noiseTexture, mirrorTint.a, noiseScale,
							reflectionStrength, tintedSkyboxTexture, fresnelSharpness);   // ← NEW
						break;

					case EffectMode.FrostEffect:
						noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
						isTextureDynamic = true;
						effectMaterial = MaterialUtils.CreateFrostOpaqueMaterial(
							mirrorTint, frostDepth, effectRT, noiseTexture, noiseStrength,
							reflectionStrength, tintedSkyboxTexture, fresnelSharpness);   // ← NEW
						break;

					case EffectMode.Water:
						effectMaterial = MaterialUtils.CreateWaterMaterialOpaque(
							mirrorTint, effectRT, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset,
							reflectionStrength, tintedSkyboxTexture, fresnelSharpness);
						break;

					//case EffectMode.OceanEffect:
					//	noiseTexture = WangTileGenerator.GenerateWangTileAtlas();
					//	isTextureDynamic = true;
					//	effectMaterial = MaterialUtils.CreateOceanOpaqueMaterial(
					//		mirrorTint, rippleSpeed, rippleAmplitude, rippleFrequency, rippleOffset,
					//		frostDepth, noiseStrength, frostThreshold, frostFadeRange, null, noiseTexture);
					//	break;

					default:
						effectMaterial = null;
						var defaultData = mainCamera?.GetComponent<UniversalAdditionalCameraData>();
						if (defaultData != null)
						{
							defaultData.cameraStack.Clear();
							defaultData.cameraStack.Add(reflectionCamera);
						}
						break;
				}

				// Setup cameras
				if (effectMaterial)
				{
					if (textureCamera)
					{
						textureCamera.targetTexture = effectRT;
						textureCamera.enabled = true;
					}
					if (reflectionCamera) reflectionCamera.targetTexture = effectRT;
				}
				else
				{
					if (textureCamera)
					{
						textureCamera.targetTexture = null;
						textureCamera.enabled = false;
					}
					if (reflectionCamera) reflectionCamera.targetTexture = null;
				}

				effectMode = value;
			}

			// Always update live properties
			if (effectMaterial == null) return;

			switch (effectMode)
			{
				case EffectMode.PerfectMirror:
					effectMaterial.SetTexture("_MainTex", effectRT);
					effectMaterial.SetColor("_DimColor", mirrorTint);
					break;

				case EffectMode.SurfaceFilm:
					effectMaterial.SetTexture("_MainTex", effectRT);
					effectMaterial.SetColor("_DimColor", mirrorTint);
					effectMaterial.SetFloat("_FilmIntensity", mirrorTint.a);
					effectMaterial.SetFloat("_NoiseScale", noiseScale);
					if (noiseTexture) effectMaterial.SetTexture("_NoiseTex", noiseTexture);

					// Fresnel reflection
					effectMaterial.SetFloat("_ReflectionStrength", reflectionStrength);
					effectMaterial.SetFloat("_FresnelSharpness", fresnelSharpness);
					if (tintedSkyboxTexture) effectMaterial.SetTexture("_Skybox", tintedSkyboxTexture);
					break;

				case EffectMode.FrostEffect:
					effectMaterial.SetTexture("_MainTex", effectRT);
					effectMaterial.SetColor("_BaseColor", mirrorTint);
					effectMaterial.SetFloat("_Depth", frostDepth);
					effectMaterial.SetFloat("_NoiseStrength", noiseStrength);

					// Fresnel reflection
					effectMaterial.SetFloat("_ReflectionStrength", reflectionStrength);
					effectMaterial.SetFloat("_FresnelSharpness", fresnelSharpness);
					if (tintedSkyboxTexture) effectMaterial.SetTexture("_Skybox", tintedSkyboxTexture);
					if (noiseTexture) effectMaterial.SetTexture("_NoiseTex", noiseTexture);
					break;

				case EffectMode.Water:
					effectMaterial.SetTexture("_MainTex", effectRT);
					effectMaterial.SetColor("_BaseColor", mirrorTint);
					effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
					effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
					effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
					effectMaterial.SetFloat("_RippleOffset", rippleOffset);
					effectMaterial.SetFloat("_ReflectionStrength", reflectionStrength);
					effectMaterial.SetFloat("_FresnelSharpness", fresnelSharpness);
					if (tintedSkyboxTexture) effectMaterial.SetTexture("_Skybox", tintedSkyboxTexture);
					break;

				//case EffectMode.OceanEffect:
				//	effectMaterial.SetTexture("_MainTex", effectRT);
				//	effectMaterial.SetColor("_BaseColor", mirrorTint);
				//	effectMaterial.SetFloat("_RippleSpeed", rippleSpeed);
				//	effectMaterial.SetFloat("_RippleAmplitude", rippleAmplitude);
				//	effectMaterial.SetFloat("_RippleFrequency", rippleFrequency);
				//	effectMaterial.SetFloat("_RippleOffset", rippleOffset);
				//	effectMaterial.SetFloat("_DepthThreshold", 128.0f);
				//	effectMaterial.SetFloat("_FrostDepth", frostDepth);
				//	effectMaterial.SetFloat("_FrostNoiseStrength", noiseStrength);
				//	effectMaterial.SetFloat("_FrostThreshold", frostThreshold);
				//	effectMaterial.SetFloat("_FrostFadeRange", frostFadeRange);
				//	if (noiseTexture) effectMaterial.SetTexture("_NoiseTex", noiseTexture);
				//	if (tintedSkyboxTexture) effectMaterial.SetTexture("_Skybox", tintedSkyboxTexture);
				//	break;
			}
		}

		private void Update()
		{
			timeSeed += Time.deltaTime;
			if (effectMaterial && effectMaterial.HasFloat("_TimeSeed"))
				effectMaterial.SetFloat("_TimeSeed", timeSeed);

			if (mainCamera != null && mainCamera.targetTexture == null)
				CheckForScreenResize();
		}

		private void CheckForScreenResize()
		{
			if (effectRT == null) return;

			int currentWidth = Screen.width;
			int currentHeight = Screen.height;

			if (currentWidth != effectRT.width || currentHeight != effectRT.height)
			{
				if (currentWidth > 0 && currentHeight > 0)
				{
					Debug.Log($"[ReflectionEffectCamera] Screen resized from {effectRT.width}×{effectRT.height} → {currentWidth}×{currentHeight}");
					UpdateEffectRT(currentWidth, currentHeight);
				}
			}
		}

		private void LateUpdate()
		{
			if (reflectionCamera)
			{
				CopyCameraProjection(reflectionCamera, mainCamera);

				reflectionCamera.clearFlags = CameraClearFlags.Nothing;
				var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
				reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
				reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
			}

			if (textureCamera)
				CopyCameraProjection(textureCamera, mainCamera);

			static void CopyCameraProjection(Camera dst, Camera src)
			{
				dst.fieldOfView = src.fieldOfView;
				dst.nearClipPlane = src.nearClipPlane;
				dst.farClipPlane = src.farClipPlane;
				dst.orthographic = src.orthographic;
				dst.orthographicSize = src.orthographicSize;
				dst.aspect = src.aspect;
			}
		}

		public void OnValidate()
		{
			if (!isActiveAndEnabled) return;

			if (Application.isPlaying)
			{
				EffectMode modeToApply = effectMode;
				effectMode = EffectMode.Null;
				UpdateEffect(modeToApply);
			}
			else
			{
				ApplyDefaults(effectMode);
			}
		}

		public void OnRenderSettingsChanged(UnityRenderSettings renderSettings)
		{
			foreach (var childCam in GetComponentsInChildren<Camera>(true))
			{
				var overrideComp = childCam.gameObject.GetOrAddComponent<CameraRenderSettingsOverride>();
				overrideComp.OverrideSettings = renderSettings;
			}
			if (tintedSkyboxTexture) DestroyImmediate(tintedSkyboxTexture);
			tintedSkyboxTexture = CubemapUtility.GetTintedCubemapInstance(renderSettings.skybox);
			UpdateEffect(effectMode);
		}

		public RenderTexture UpdateExternalOutput(Vector2Int value)
		{
			if (!mainCamera) return null;

			mainCamera.targetTexture = null;
			if (value != null)
			{
				mainCamera.targetTexture = UpdateOutputRT(value.x, value.y);
				UpdateEffectRT(value.x, value.y);
			}
			else
				UpdateEffectRT(Screen.width, Screen.height);

			return mainCamera.targetTexture;
		}

		private void UpdateEffectRT(int targetWidth, int targetHeight)
		{
			targetWidth = Mathf.Max(16, targetWidth);
			targetHeight = Mathf.Max(16, targetHeight);

			var needsResize = effectRT == null || effectRT.width != targetWidth || effectRT.height != targetHeight;
			if (!needsResize) return;

			RenderTexture.active = null;

			if (reflectionCamera) reflectionCamera.targetTexture = null;
			if (textureCamera) textureCamera.targetTexture = null;

			if (effectRT != null)
			{
				DestroyImmediate(effectRT);
				effectRT = null;
			}

			effectRT = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
			{
				name = $"Reflection_{effectMode}",
				useMipMap = false,
				autoGenerateMips = false,
				filterMode = FilterMode.Bilinear,
				useDynamicScale = true
			};
			effectRT.Create();

			if (reflectionCamera) reflectionCamera.targetTexture = effectRT;
			if (textureCamera)
			{
				textureCamera.targetTexture = effectRT;
				textureCamera.enabled = effectMaterial != null;
			}

			if (effectMaterial && effectMaterial.HasProperty("_MainTex"))
				effectMaterial.SetTexture("_MainTex", effectRT);
		}

		private RenderTexture UpdateOutputRT(int targetWidth, int targetHeight)
		{
			var needsResize = outputRT == null || outputRT.width != targetWidth || outputRT.height != targetHeight;
			if (needsResize)
			{
				if (outputRT != null)
				{
					DestroyImmediate(outputRT);
					outputRT = null;
				}

				outputRT = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32)
				{
					name = $"Output_{effectMode}",
					useMipMap = false,
					autoGenerateMips = false,
					filterMode = FilterMode.Bilinear,
					useDynamicScale = true
				};
				outputRT.Create();
			}
			return outputRT;
		}

		void OnDestroy()
		{
			RenderTexture.active = null;
			if (mainCamera) mainCamera.targetTexture = null;
			if (reflectionCamera) reflectionCamera.targetTexture = null;
			if (textureCamera) textureCamera.targetTexture = null;

			if (effectMaterial) DestroyImmediate(effectMaterial);
			if (effectMesh) DestroyImmediate(effectMesh);
			if (effectRT) DestroyImmediate(effectRT);
			if (outputRT) DestroyImmediate(outputRT);
			if (isTextureDynamic && noiseTexture) DestroyImmediate(noiseTexture);

			if (reflectionCamera) DestroyImmediate(reflectionCamera.gameObject);
			if (textureCamera) DestroyImmediate(textureCamera.gameObject);

			if (tintedSkyboxTexture) DestroyImmediate(tintedSkyboxTexture);
		}

		public static EffectMode ParseEffectMode(string effectString)
		{
			if (string.IsNullOrWhiteSpace(effectString)) return EffectMode.Null;

			if (Enum.TryParse<EffectMode>(effectString, ignoreCase: true, out var mode))
				return mode == EffectMode.Null ? EffectMode.Debug : mode;

			return effectString.ToLowerInvariant() switch
			{
				"debug" => EffectMode.Debug,
				"mirror" => EffectMode.PerfectMirror,
				"perfectmirror" => EffectMode.PerfectMirror,
				"film" => EffectMode.SurfaceFilm,
				"surfacefilm" => EffectMode.SurfaceFilm,
				"frost" => EffectMode.FrostEffect,
				"frosteffect" => EffectMode.FrostEffect,
				"water" => EffectMode.Water,
				"watereffect" => EffectMode.Water,
				//"ocean" => EffectMode.OceanEffect,
				//"oceaneffect" => EffectMode.OceanEffect,
				_ => EffectMode.Null
			};
		}

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
				//EffectMode.OceanEffect => "Ocean",
				_ => null
			};
		}
	}
}