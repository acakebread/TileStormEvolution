using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[Serializable]
	public class IconAtlas : GridAtlas
	{
		private readonly bool _includeGround;
		private readonly float _yaw;
		private readonly float _pitch;
		private readonly Color _backgroundColor;

		public IconAtlas(
			int cellSize,
			int columns,
			IEnumerable<Definition> filteredDefs,
			bool includeGround = false,
			Color? background = null,
			float yaw = 35f,
			float pitch = 30f)
		{
			_includeGround = includeGround;
			_yaw = yaw;
			_pitch = pitch;
			_backgroundColor = background ?? new Color(0, 0, 0, 0); // default transparent

			Initialize(
				cellSize: cellSize,
				columns: columns,
				itemsToRender: filteredDefs ?? Enumerable.Empty<Definition>(),
				background: _backgroundColor);
		}

		protected override IDisposable CreateRenderer(int cellSize, Color background)
		{
			// We ignore the passed background here and use our stored one
			// (GridAtlas might pass it, but we want consistent transparent bg most times)
			return new ReusableIconRenderer(
				size: cellSize,
				background: _backgroundColor,
				includeGround: _includeGround,
				initialYaw: _yaw,
				initialPitch: _pitch);
		}

		protected override Texture2D GenerateIcon(IDisposable renderer, object item, int index)
		{
			if (item is not Definition def) return null;

			var r = (ReusableIconRenderer)renderer;

			// Optional: override light direction to fixed world-space (recommended for atlas consistency)
			// This requires a small change in ReusableIconRenderer — see note below
			// For now we keep camera-relative as in your working version

			return r.RenderIcon(def, yaw: _yaw, pitch: _pitch);
		}

		// Optional helper if you want to regenerate the atlas later
		public void Refresh(IEnumerable<Definition> newFilteredDefs)
		{
			// If MassiveHadronLtd.GridAtlas has a way to rebuild, call it
			// Otherwise dispose & recreate the atlas instance externally
			// This method is just a placeholder — implement according to your needs
			Debug.Log($"IconAtlas refresh requested for {newFilteredDefs.Count()} definitions");
		}
	}

	public class ReusableIconRenderer : IDisposable
	{
		private readonly GameObject _root;
		private readonly CommandRenderCamera _cameraWrapper;
		private readonly RenderTexture _rt;
		private readonly CommandRenderScene _scene;
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

			var lightObj = new GameObject("IconLight") { hideFlags = HideFlags.HideAndDontSave };
			lightObj.transform.SetParent(_root.transform, false);
			_iconLight = lightObj.AddComponent<Light>();
			_iconLight.type = LightType.Directional;
			_iconLight.intensity = 1.2f;//overwritten later so ignore this value
			_iconLight.color = new Color(1f, 0.98f, 0.95f);
			_iconLight.shadows = LightShadows.None;
			_iconLight.enabled = false;

			_rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
			{
				antiAliasing = 4,
				filterMode = FilterMode.Bilinear,
				autoGenerateMips = false,
				name = "SharedIconRT"
			};
			_rt.Create();

			_cameraWrapper = new CommandRenderCamera(
				name: "SharedIconCamera",
				targetRT: _rt,
				background: background == default ? new Color(0, 0, 0, 0) : background,
				fov: 50f,
				desiredParent: _root.transform
			);

			//doesn't work yet!!!
			//_cameraWrapper.overrideSettings = new(
			//ambientMode: UnityEngine.Rendering.AmbientMode.Flat,
			//ambientLight: Color.purple,
			//ambientIntensity: 1f,
			//skybox: RenderSettings.skybox,
			//ambientProbe: default,
			//subtractiveShadowColor: RenderSettings.subtractiveShadowColor);

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

		public void SetCameraRotation(float yaw, float pitch)
		{
			var rot = Quaternion.Euler(pitch, yaw, 0f);
			_cameraWrapper.rotation = rot;
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
			var dist = radius / Mathf.Tan(_cameraWrapper.fieldOfView * 0.5f * Mathf.Deg2Rad);

			_cameraWrapper.position = center - _cameraWrapper.rotation * Vector3.forward * dist;

			_iconLight.transform.rotation = _cameraWrapper.rotation * Quaternion.Euler(-35f, 45f, 0f);
			_iconLight.intensity = 10f;// doesn't seem to have any effect
			_iconLight.enabled = true;

			_cameraWrapper.Render();

			_iconLight.enabled = false;

			// Readback from the final render
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

			_cameraWrapper?.Destroy();
			if (_root != null)
			{
				UnityEngine.Object.DestroyImmediate(_root);
			}
		}
	}
}