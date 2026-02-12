using System;
using System.Collections.Generic;
using UnityEngine;
using MassiveHadronLtd;
using UnityEngine.Rendering;

namespace ClassicTilestorm
{
	public class ReusableIconRenderer : IDisposable
	{
		private readonly GameObject _root;
		private readonly Camera _camera;
		private readonly RenderTexture _rt;
		private readonly CommandRenderScene _scene;
		private readonly CommandCameraHook _hook;
		private readonly Light _iconLight;

		private CommandRenderModelData _groundModel;

		public ReusableIconRenderer(
			int size = 128,
			Color background = default,
			bool includeGround = false,
			float initialYaw = 35f,
			float initialPitch = 30f)
		{
			if (size <= 0) throw new ArgumentException("Size must be > 0");

			_root = new GameObject("ReusableIconRenderer") { hideFlags = HideFlags.HideAndDontSave };

			// Create dedicated directional light for icons
			var lightObj = new GameObject("IconLight") { hideFlags = HideFlags.HideAndDontSave };
			lightObj.transform.SetParent(_root.transform, false);
			_iconLight = lightObj.AddComponent<Light>();
			_iconLight.type = LightType.Directional;
			_iconLight.intensity = 1.2f;
			_iconLight.color = new Color(1f, 0.98f, 0.95f); // slightly warm white
			_iconLight.shadows = LightShadows.None;
			_iconLight.enabled = false; // off by default

			_rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
			{
				antiAliasing = 4,
				filterMode = FilterMode.Bilinear,
				autoGenerateMips = false,
				name = "SharedIconRT"
			};
			_rt.Create();

			var camObj = new GameObject("SharedIconCamera") { hideFlags = HideFlags.HideAndDontSave };
			camObj.transform.SetParent(_root.transform, false);

			_camera = camObj.AddComponent<Camera>();
			_camera.enabled = false;
			_camera.clearFlags = CameraClearFlags.SolidColor;
			_camera.backgroundColor = background == default ? new Color(0, 0, 0, 0) : background;
			_camera.fieldOfView = 50f;
			_camera.nearClipPlane = 0.03f;
			_camera.farClipPlane = 50f;
			_camera.targetTexture = _rt;
			_camera.aspect = 1f;
			_camera.cullingMask = 0;

			_scene = new CommandRenderScene();
			_hook = camObj.AddComponent<CommandCameraHook>();
			_hook.Provider = _scene;

			if (includeGround)
			{
				var quadMesh = MeshUtils.GenerateQuadXZ(3f, 1f, "SharedIconGround");
				var groundMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
				{
					hideFlags = HideFlags.HideAndDontSave,
					color = Color.white
				};
				groundMat.SetTexture("_BaseMap", Texture2D.whiteTexture);

				_groundModel = new CommandRenderModelData(quadMesh, new[] { groundMat }, Matrix4x4.identity, 0);
			}

			SetCameraRotation(initialYaw, initialPitch);
		}

		public void SetCameraRotation(float yaw, float pitch)
		{
			var rot = Quaternion.Euler(pitch, yaw, 0f);
			_camera.transform.rotation = rot;
		}

		public Texture2D RenderIcon(Definition def, float yaw = -1f, float pitch = -1f)
		{
			if (def == null || string.IsNullOrEmpty(def.model))
				return null;

			if (yaw >= 0 || pitch >= 0)
			{
				SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);
			}

			var modelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
			if (modelData == null || modelData.meshInstances.Count == 0)
			{
				Debug.LogWarning($"No mesh instances for {def.name}");
				return null;
			}

			CommandRenderModelData[] modelsToRender;
			if (_groundModel != null)
			{
				float lowestY = modelData.bounds.min.y;
				var adjustedGround = new CommandRenderModelData(
					_groundModel.meshInstances[0].mesh,
					_groundModel.meshInstances[0].materials,
					Matrix4x4.Translate(Vector3.up * (lowestY - 0.02f)),
					0);

				modelsToRender = new[] { adjustedGround, modelData };
			}
			else
			{
				modelsToRender = new[] { modelData };
			}

			_scene.SetModels(modelsToRender);

			var center = modelData.bounds.center;
			var radius = modelData.bounds.extents.magnitude * 1.2f;
			var dist = radius / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

			_camera.transform.position = center - _camera.transform.rotation * Vector3.forward * dist;

			// ────────────────────────────────────────────────
			// Setup consistent lighting for this render
			// ────────────────────────────────────────────────

			var prevAmbientMode = RenderSettings.ambientMode;
			var prevAmbientColor = RenderSettings.ambientLight;
			var prevAmbientIntensity = RenderSettings.ambientIntensity;

			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.ambientLight = Color.black;
			RenderSettings.ambientIntensity = 0f;

			// Disable other directional lights
			var sceneLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
			var disabledLights = new List<Light>();
			foreach (var l in sceneLights)
			{
				if (l != _iconLight && l.type == LightType.Directional && l.enabled)
				{
					l.enabled = false;
					disabledLights.Add(l);
				}
			}

			// Enable icon light
			_iconLight.transform.rotation = _camera.transform.rotation * Quaternion.Euler(-30f, 45f, 0f);
			_iconLight.intensity = 1.3f;
			_iconLight.enabled = true;

			_camera.Render();

			// Restore
			_iconLight.enabled = false;
			foreach (var l in disabledLights) l.enabled = true;

			RenderSettings.ambientMode = prevAmbientMode;
			RenderSettings.ambientLight = prevAmbientColor;
			RenderSettings.ambientIntensity = prevAmbientIntensity;

			_iconLight.enabled = false;

			// Extract texture
			var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = $"Icon_{def.name ?? "unnamed"}"
			};

			var prevActive = RenderTexture.active;
			RenderTexture.active = _rt;
			tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
			tex.Apply();
			RenderTexture.active = prevActive;

			return tex;
		}

		public void Dispose()
		{
			if (_rt != null)
			{
				_rt.Release();
			}

			if (_root != null)
			{
				UnityEngine.Object.DestroyImmediate(_root);
			}
		}
	}
}