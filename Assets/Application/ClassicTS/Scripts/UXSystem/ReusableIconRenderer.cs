using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class ReusableIconRenderer : IDisposable
	{
		private readonly GameObject _root;
		private readonly CommandRenderCamera _cameraWrapper;
		private readonly RenderTexture _rt;
		private readonly CommandRenderScene _scene;
		private CommandRenderModelData _groundModel;

		public ReusableIconRenderer(int size = 128, Color background = default, bool includeGround = false, float initialYaw = 35f, float initialPitch = 30f)
		{
			if (size <= 0) throw new ArgumentException("Size must be > 0");

			_root = new GameObject("ReusableIconRenderer") { hideFlags = HideFlags.HideAndDontSave };

			_rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
			{
				antiAliasing = 4,
				filterMode = FilterMode.Bilinear,
				autoGenerateMips = false,
				name = "IconRT"
			};
			_rt.Create();

			_cameraWrapper = new (name: "SharedIconCamera", targetRT: _rt, background: background == default ? new Color(0, 0, 0, 0) : background, fov: 50f, desiredParent: _root.transform);
			_scene = new CommandRenderScene();
			_cameraWrapper.AssignCommandProvider(_scene);

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

		public void SetCameraRotation(float yaw, float pitch) => _cameraWrapper.rotation = Quaternion.Euler(pitch, yaw, 0f);

		public Texture2D RenderIcon(Definition def, float yaw = -1f, float pitch = -1f)
		{
			if (null == def || string.IsNullOrEmpty(def.model))
				return RenderMissingIcon(_rt.width, _rt.height, new Color32(51, 128, 255, 255));

			// ====================== PRIME THE SCENE HERE ======================
			if (yaw >= 0 || pitch >= 0)
				SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);

			// ====================== SET LIGHT FROM CAMERA ======================
			if (null != _scene)
			{
				_scene.MainLightDirection = _cameraWrapper.rotation * Vector3.back;
				_scene.MainLightColor = new Color(1.15f, 1.1f, 1.05f);
				_scene.MainLightIntensity = 1.5f;
				_scene.AmbientColor = Color.white;
				_scene.AmbientIntensity = 0f;
			}

			var modelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
			if (null == modelData || modelData.meshInstances.Count == 0)
			{
				Debug.LogWarning($"No mesh instances for {def.name}");
				return RenderMissingIcon(_rt.width, _rt.height, new Color32(255, 32, 32, 255));//error colour
			}

			CommandRenderModelData[] modelsToRender = _groundModel != null ? new[] { CreateAdjustedGround(modelData), modelData } : new[] { modelData };

			_scene.SetModels(modelsToRender);

			// Frame the model
			var center = modelData.bounds.center;
			var radius = modelData.bounds.extents.magnitude * 1.2f;
			var dist = radius / Mathf.Tan(_cameraWrapper.fieldOfView * 0.5f * Mathf.Deg2Rad);
			_cameraWrapper.position = center - _cameraWrapper.rotation * Vector3.forward * dist;

			_cameraWrapper.Render();

			// Readback
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

			static Texture2D RenderMissingIcon(int width, int height, Color32 color)
			{
				var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
				var pixels = new Color32[result.width * result.height];
				var margin = result.width / 4;
				var thick = result.width / 16;

				for (var py = 0; py < result.height; py++)
					for (var px = 0; px < result.width; px++)
					{
						var inH = px >= margin && px < result.width - margin;
						var inV = py >= margin && py < result.height - margin;
						if (!inH || !inV) continue;

						if ((px < margin + thick) || (px >= result.width - margin - thick && px < result.width - margin) ||
							(py < margin + thick) || (py >= result.height - margin - thick && py < result.height - margin))
							pixels[py * result.width + px] = color;
					}

				result.SetPixels32(pixels);
				return result;
			}

			CommandRenderModelData CreateAdjustedGround(CommandRenderModelData modelData) =>
			new(_groundModel.meshInstances[0].mesh, _groundModel.meshInstances[0].materials, Matrix4x4.Translate(Vector3.up * (modelData.bounds.min.y - 0.02f)), 0);
		}

		public void Dispose()
		{
			if (_rt) _rt.Release();
			_cameraWrapper?.Destroy();
			if (_root) UnityEngine.Object.DestroyImmediate(_root);
		}
	}
}
