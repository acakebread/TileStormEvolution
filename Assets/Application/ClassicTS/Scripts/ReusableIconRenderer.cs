using System;
using UnityEngine;
using MassiveHadronLtd;
using System.Collections;

namespace ClassicTilestorm
{
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
			_iconLight.intensity = 1.2f;
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
			_iconLight.intensity = 1.2f;
			_iconLight.enabled = true;

			// CRITICAL: Multiple renders in the same call — gives URP time to initialize lighting
			for (int i = 0; i < 5; i++)  // 5 passes is usually enough; tune down to 3 if perf is bad
			{
				_cameraWrapper.Render();
			}

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

//using System;
//using UnityEngine;
//using MassiveHadronLtd;

//namespace ClassicTilestorm
//{
//	public class ReusableIconRenderer : IDisposable
//	{
//		private readonly GameObject _root;
//		private readonly CommandRenderCamera _cameraWrapper;
//		private readonly RenderTexture _rt;
//		private readonly CommandRenderScene _scene;
//		private readonly Light _iconLight;

//		private CommandRenderModelData _groundModel;

//		public ReusableIconRenderer(
//			int size = 128,
//			Color background = default,
//			bool includeGround = false,
//			float initialYaw = 35f,
//			float initialPitch = 30f)
//		{
//			if (size <= 0) throw new ArgumentException("Size must be > 0");

//			_root = new GameObject("ReusableIconRenderer") { hideFlags = HideFlags.HideAndDontSave };

//			// Real directional light for icons
//			var lightObj = new GameObject("IconLight") { hideFlags = HideFlags.HideAndDontSave };
//			lightObj.transform.SetParent(_root.transform, false);
//			_iconLight = lightObj.AddComponent<Light>();
//			_iconLight.type = LightType.Directional;
//			_iconLight.intensity = 1.2f;
//			_iconLight.color = new Color(1f, 0.98f, 0.95f);
//			_iconLight.shadows = LightShadows.None;
//			_iconLight.enabled = false;

//			_rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
//			{
//				antiAliasing = 4,
//				filterMode = FilterMode.Bilinear,
//				autoGenerateMips = false,
//				name = "SharedIconRT"
//			};
//			_rt.Create();

//			// Use CommandRenderCamera — same as previews
//			_cameraWrapper = new CommandRenderCamera(
//				name: "SharedIconCamera",
//				targetRT: _rt,
//				background: background == default ? new Color(0, 0, 0, 0) : background,
//				fov: 50f,
//				desiredParent: _root.transform
//			);

//			_scene = new CommandRenderScene();
//			_cameraWrapper.AssignCommandProvider(_scene);

//			if (includeGround)
//			{
//				var quadMesh = MeshUtils.GenerateQuadXZ(3f, 1f, "SharedIconGround");
//				var groundMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
//				{
//					hideFlags = HideFlags.HideAndDontSave,
//					color = Color.white
//				};
//				groundMat.SetTexture("_BaseMap", Texture2D.whiteTexture);

//				_groundModel = new CommandRenderModelData(quadMesh, new[] { groundMat }, Matrix4x4.identity, 0);
//			}

//			SetCameraRotation(initialYaw, initialPitch);
//		}

//		public void SetCameraRotation(float yaw, float pitch)
//		{
//			var rot = Quaternion.Euler(pitch, yaw, 0f);
//			_cameraWrapper.rotation = rot;
//		}

//		public Texture2D RenderIcon(Definition def, float yaw = -1f, float pitch = -1f)
//		{
//			if (def == null || string.IsNullOrEmpty(def.model))
//				return null;

//			if (yaw >= 0 || pitch >= 0)
//			{
//				SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);
//			}

//			var modelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
//			if (modelData == null || modelData.meshInstances.Count == 0)
//			{
//				Debug.LogWarning($"No mesh instances for {def.name}");
//				return null;
//			}

//			CommandRenderModelData[] modelsToRender;
//			if (_groundModel != null)
//			{
//				float lowestY = modelData.bounds.min.y;
//				var adjustedGround = new CommandRenderModelData(
//					_groundModel.meshInstances[0].mesh,
//					_groundModel.meshInstances[0].materials,
//					Matrix4x4.Translate(Vector3.up * (lowestY - 0.02f)),
//					0);

//				modelsToRender = new[] { adjustedGround, modelData };
//			}
//			else
//			{
//				modelsToRender = new[] { modelData };
//			}

//			_scene.SetModels(modelsToRender);

//			var center = modelData.bounds.center;
//			var radius = modelData.bounds.extents.magnitude * 1.2f;
//			var dist = radius / Mathf.Tan(_cameraWrapper.fieldOfView * 0.5f * Mathf.Deg2Rad);

//			_cameraWrapper.position = center - _cameraWrapper.rotation * Vector3.forward * dist;

//			// Real light setup
//			_iconLight.transform.rotation = _cameraWrapper.rotation * Quaternion.Euler(-35f, 45f, 0f);
//			_iconLight.intensity = 1.2f;
//			_iconLight.enabled = true;

//			_cameraWrapper.Render();

//			// Cleanup
//			_iconLight.enabled = false;

//			// Readback
//			var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false)
//			{
//				filterMode = FilterMode.Bilinear,
//				wrapMode = TextureWrapMode.Clamp,
//				name = $"Icon_{def.name ?? "unnamed"}"
//			};

//			var prevActive = RenderTexture.active;
//			RenderTexture.active = _rt;
//			tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
//			tex.Apply();
//			RenderTexture.active = prevActive;

//			return tex;
//		}

//		public void Dispose()
//		{
//			if (_rt != null)
//			{
//				_rt.Release();
//			}

//			_cameraWrapper?.Destroy();
//			if (_root != null)
//			{
//				UnityEngine.Object.DestroyImmediate(_root);
//			}
//		}
//	}
//}