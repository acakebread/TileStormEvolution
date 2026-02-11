using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class ReusableIconRenderer : IDisposable
	{
		private readonly GameObject _root;
		private readonly Camera _camera;
		private readonly RenderTexture _rt;
		private readonly CommandRenderScene _scene;
		private readonly CommandCameraHook _hook;

		private CommandRenderModelData _currentModel;
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

			// Optional shared ground (reused for all icons if enabled)
			if (includeGround)
			{
				var quadMesh = MeshUtils.GenerateQuadXZ(3f, 1f, "SharedIconGround");
				var groundMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
				{
					hideFlags = HideFlags.HideAndDontSave,
					color = Color.white
				};
				groundMat.SetTexture("_BaseMap", Texture2D.whiteTexture);

				_groundModel = new CommandRenderModelData(quadMesh, new[] { groundMat }, Matrix4x4.identity);
			}

			// Set initial rotation (can be overridden per icon if desired)
			SetCameraRotation(initialYaw, initialPitch);
		}

		public void SetCameraRotation(float yaw, float pitch)
		{
			var rot = Quaternion.Euler(pitch, yaw, 0f);
			_camera.transform.rotation = rot;
			// position will be updated in RenderIcon() based on model bounds
		}

		public Texture2D RenderIcon(Definition def, float yaw = -1f, float pitch = -1f)
		{
			if (def == null || string.IsNullOrEmpty(def.model))
				return null;

			// Optional: allow per-icon rotation override
			if (yaw >= 0 || pitch >= 0)
			{
				SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);
			}

			// Clean previous model
			if (_currentModel != null)
			{
				_currentModel = null;
			}
			var modelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
			if (modelData == null || modelData.meshInstances.Count == 0)
			{
				Debug.LogWarning($"No mesh instances for {def.name}");
				return null;
			}

			_currentModel = modelData;

			// Adjust ground position if used
			CommandRenderModelData[] modelsToRender;
			if (_groundModel != null)
			{
				float lowestY = modelData.bounds.min.y;
				var adjustedGround = new CommandRenderModelData(
					_groundModel.meshInstances[0].mesh,
					_groundModel.meshInstances[0].materials,
					Matrix4x4.Translate(Vector3.up * (lowestY - 0.02f)));

				modelsToRender = new[] { adjustedGround, modelData };
			}
			else
			{
				modelsToRender = new[] { modelData };
			}

			_scene.SetModels(modelsToRender);

			// Position camera
			var center = modelData.bounds.center;
			var radius = modelData.bounds.extents.magnitude * 1.2f;
			var dist = radius / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

			_camera.transform.position = center - _camera.transform.rotation * Vector3.forward * dist;

			// Render!
			_camera.Render();//this call is surprisingly slow

			var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = $"Icon_{def.name ?? "unnamed"}"
			};

			var prev = RenderTexture.active;
			RenderTexture.active = _rt;
			tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
			tex.Apply();
			RenderTexture.active = prev;

			return tex;
		}

		public void Dispose()
		{
			if (_currentModel != null)
			{
				_currentModel = null;
			}

			if (_groundModel != null)
			{
				_groundModel = null;
			}

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