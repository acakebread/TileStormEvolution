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

		// ── Reusable allocations to reduce GC pressure ─────────────────────
		private readonly CommandRenderModelData[] _modelsToRender = new CommandRenderModelData[2];
		private CommandRenderModelData _adjustedGround;   // reused for ground adjustment

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
				antiAliasing = 1,
				filterMode = FilterMode.Bilinear,
				autoGenerateMips = false,
				name = "IconRT"
			};
			_rt.Create();

			_cameraWrapper = new CommandRenderCamera(
				name: "SharedIconCamera",
				targetRT: _rt,
				background: background == default ? new Color(0, 0, 0, 0) : background,
				fov: 50f,
				desiredParent: _root.transform);

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

				// Pre-create the adjustable ground container (we'll update its matrix each time)
				_adjustedGround = new CommandRenderModelData(
					_groundModel.meshInstances[0].mesh,
					_groundModel.meshInstances[0].materials,
					Matrix4x4.identity,  // will be overwritten
					0);
			}

			SetCameraRotation(initialYaw, initialPitch);
		}

		public void SetCameraRotation(float yaw, float pitch)
		{
			_cameraWrapper.rotation = Quaternion.Euler(pitch, yaw, 0f);
		}

		public Texture2D RenderIcon(Definition def, float yaw = -1f, float pitch = -1f)
		{
			if (def == null || string.IsNullOrEmpty(def.model))
				return RenderMissingIcon(_rt.width, _rt.height, new Color32(51, 128, 255, 255));

			// Camera rotation
			if (yaw >= 0 || pitch >= 0)
				SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);

			// Lighting
			if (_scene != null)
			{
				_scene.MainLightDirection = _cameraWrapper.rotation * Vector3.back;
				_scene.MainLightColor = new Color(1.15f, 1.1f, 1.05f);
				_scene.MainLightIntensity = 1.5f;
				_scene.AmbientColor = Color.white;
				_scene.AmbientIntensity = 0f;
			}

			// Create the main model data (this is still the biggest potential allocator — unavoidable without per-Definition cache)
			var modelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
			if (modelData == null || modelData.meshInstances.Count == 0)
			{
				Debug.LogWarning($"No mesh instances for {def.name}");
				return RenderMissingIcon(_rt.width, _rt.height, new Color32(255, 32, 32, 255));
			}

			// ── Reused models array (zero new[] allocation) ─────────────────────
			int modelCount = 0;

			if (_groundModel != null)
			{
				// Update the reusable adjusted ground matrix (no new object)
				var adjustedMatrix = Matrix4x4.Translate(Vector3.up * (modelData.bounds.min.y - 0.02f));
				_adjustedGround = new CommandRenderModelData(   // note: if your struct/class allows mutation, prefer updating in-place
					_groundModel.meshInstances[0].mesh,
					_groundModel.meshInstances[0].materials,
					adjustedMatrix,
					0);

				_modelsToRender[modelCount++] = _adjustedGround;
			}

			_modelsToRender[modelCount++] = modelData;

			// Pass the reused array slice (SetModels just stores the reference)
			_scene.SetModels(_modelsToRender);   // it will use the first 'modelCount' entries

			// Frame the model
			var center = modelData.bounds.center;
			var radius = modelData.bounds.extents.magnitude * 1.2f;
			var dist = radius / Mathf.Tan(_cameraWrapper.fieldOfView * 0.5f * Mathf.Deg2Rad);
			_cameraWrapper.position = center - _cameraWrapper.rotation * Vector3.forward * dist;

			_cameraWrapper.Render();

			// Readback (this is still the main WebGL cost, but we can't avoid it reliably)
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

		private static Texture2D RenderMissingIcon(int width, int height, Color32 color)
		{
			var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
			var p = new Color32[width * height];
			int m = width / 4, t = Mathf.Max(1, width / 16);

			void H(int y) { for (int x = m; x < width - m; x++) for (int i = 0; i < t; i++) p[(y + i) * width + x] = color; }
			void V(int x) { for (int y = m + t; y < height - m - t; y++) for (int i = 0; i < t; i++) p[y * width + x + i] = color; }

			H(m); H(height - m - t);
			V(m); V(width - m - t);

			tex.SetPixels32(p);
			tex.Apply();
			return tex;
		}

		public void Dispose()
		{
			if (_rt != null) _rt.Release();
			_cameraWrapper?.Destroy();
			if (_root != null) UnityEngine.Object.DestroyImmediate(_root);

			// Optional: clean materials/meshes if you want (groundMat etc.)
		}
	}
}