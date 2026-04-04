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
			}

			SetCameraRotation(initialYaw, initialPitch);
		}

		public void SetCameraRotation(float yaw, float pitch)
		{
			_cameraWrapper.rotation = Quaternion.Euler(pitch, yaw, 0f);
		}

		public Texture2D RenderIcon(Definition def, float yaw = -1f, float pitch = -1f)
		{
			//Shader.SetGlobalInt("_AdditionalLightsCount", 0);

			// ====================== PRIME THE SCENE HERE ======================
			if (yaw >= 0 || pitch >= 0)
				SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);

			// ====================== SET LIGHT FROM CAMERA ======================
			if (_scene != null)
			{
				// This is the line that should make the light come from the camera
				_scene.MainLightDirection = _cameraWrapper.rotation * Vector3.back;   // back = toward the model from camera

				// Alternative tests - try these one at a time:
				// _scene.MainLightDirection = -_cameraWrapper.rotation * Vector3.forward; // same as above
				// _scene.MainLightDirection = _cameraWrapper.rotation * new Vector3(0.2f, -1f, -0.3f); // angled from above

				_scene.MainLightColor = new Color(1.15f, 1.1f, 1.05f);
				_scene.MainLightIntensity = 1.5f;

				_scene.AmbientColor = Color.black;    // disable ambient for testing
				_scene.AmbientIntensity = 0f;
			}

			if (def == null || string.IsNullOrEmpty(def.model))
			{
				var missing = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
				RenderMissingModelIcon(missing);
				return missing;
			}

			var modelData = RenderModelFactory.Create(def, Vector3.zero, Quaternion.identity, Vector3.one);
			if (modelData == null || modelData.meshInstances.Count == 0)
			{
				Debug.LogWarning($"No mesh instances for {def.name}");
				return null;
			}

			CommandRenderModelData[] modelsToRender = _groundModel != null
				? new[] { CreateAdjustedGround(modelData), modelData }
				: new[] { modelData };

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
		}

		private CommandRenderModelData CreateAdjustedGround(CommandRenderModelData modelData)
		{
			float lowestY = modelData.bounds.min.y;
			return new CommandRenderModelData(
				_groundModel.meshInstances[0].mesh,
				_groundModel.meshInstances[0].materials,
				Matrix4x4.Translate(Vector3.up * (lowestY - 0.02f)),
				0);
		}

		private void RenderMissingModelIcon(Texture2D dst)
		{
			// your original missing icon code (unchanged)
			var pixels = new Color32[dst.width * dst.height];
			var margin = dst.width / 4;
			var thick = dst.width / 16;
			var color = new Color32(51, 128, 255, 255);

			for (var py = 0; py < dst.height; py++)
				for (var px = 0; px < dst.width; px++)
				{
					var inH = px >= margin && px < dst.width - margin;
					var inV = py >= margin && py < dst.height - margin;
					if (!inH || !inV) continue;

					if ((px < margin + thick) || (px >= dst.width - margin - thick && px < dst.width - margin) ||
						(py < margin + thick) || (py >= dst.height - margin - thick && py < dst.height - margin))
						pixels[py * dst.width + px] = color;
				}

			dst.SetPixels32(pixels);
		}

		public void Dispose()
		{
			if (_rt != null) _rt.Release();
			_cameraWrapper?.Destroy();
			if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
		}
	}
}

//using System;
//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using MassiveHadronLtd;

//namespace ClassicTilestorm // or your namespace
//{
//	public class ReusableIconRenderer : IDisposable
//	{
//		private readonly GameObject _root;
//		private readonly CommandRenderCamera _cameraWrapper;
//		private readonly RenderTexture _rt;
//		private readonly CommandRenderScene _scene;

//		private readonly Light _iconLight; // kept for easy direction control (optional)

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

//			_rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
//			{
//				antiAliasing = 4,
//				filterMode = FilterMode.Bilinear,
//				autoGenerateMips = false,
//				name = "IconRT"
//			};
//			_rt.Create();

//			_cameraWrapper = new CommandRenderCamera(
//				name: "SharedIconCamera",
//				targetRT: _rt,
//				background: background == default ? new Color(0, 0, 0, 0) : background,
//				fov: 50f,
//				desiredParent: _root.transform);

//			// Optional real Light component (for debugging or future use)
//			var lightObj = new GameObject("IconKeyLight") { hideFlags = HideFlags.HideAndDontSave };
//			lightObj.transform.SetParent(_root.transform, false);
//			_iconLight = lightObj.AddComponent<Light>();
//			_iconLight.type = LightType.Directional;
//			_iconLight.intensity = 1.8f;
//			_iconLight.color = new Color(1f, 0.98f, 0.95f);
//			_iconLight.shadows = LightShadows.None;

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
//			//var rot = Quaternion.Euler(pitch, yaw, 0f);
//			//_cameraWrapper.rotation = rot;

//			//// Make the light follow the camera angle (consistent "key light" from top-rightish)
//			//if (_iconLight != null)
//			//	_iconLight.transform.rotation = rot * Quaternion.Euler(-30f, 45f, 0f);

//			if (yaw >= 0 || pitch >= 0)
//				SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);

//			// Light direction tied to camera view
//			if (_scene != null)
//			{
//				_scene.LightOffsetFromCamera = new Vector3(0.3f, -0.8f, -0.5f); // tweak these 3 numbers:
//																				// X = left/right, Y = up/down (negative = from above), Z = forward/back

//				_scene.MainLightColor = new Color(1.1f, 1.05f, 1.0f);
//				_scene.MainLightIntensity = 3.5f;   // increase if icons are too dark
//			}

//			// Frame the model (your existing code)
//			var center = modelData.bounds.center;
//			var radius = modelData.bounds.extents.magnitude * 1.2f;
//			var dist = radius / Mathf.Tan(_cameraWrapper.fieldOfView * 0.5f * Mathf.Deg2Rad);
//			_cameraWrapper.position = center - _cameraWrapper.rotation * Vector3.forward * dist;
//		}

//		public Texture2D RenderIcon(Definition def, float yaw = -1f, float pitch = -1f)
//		{
//			//if (yaw >= 0 || pitch >= 0)
//			//	SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);

//			//if (_scene != null)
//			//	_scene.MainLightDirection = _cameraWrapper.rotation * Vector3.forward;// new Vector3(0.4f, -0.8f, -0.3f);

//			if (yaw >= 0 || pitch >= 0)
//				SetCameraRotation(yaw >= 0 ? yaw : 35f, pitch >= 0 ? pitch : 30f);

//			// Control light direction here — experiment with different offsets
//			if (_scene != null)
//			{
//				// Option 1: fixed nice 3/4 key light (recommended for consistent atlas icons)
//				//_scene.MainLightDirection = new Vector3(0.5f, -0.8f, -0.4f);
//				_scene.MainLightDirection = new Vector3(0f, 1f, -1f).normalized;
//				//_scene.MainLightDirection = _cameraWrapper.transform.forward;

//				// _scene.MainLightIntensity = 0f;
//				// _scene.AmbientIntensity = 3.0f;

//				_scene.MainLightIntensity = 1f;
//				_scene.AmbientIntensity = 1f;

//				//public Color MainLightColour{ get; set; } = Color.white;
//				//public float MainLightIntensity { get; set; } = 1f;

//				//public Color AmbientColour { get; set; } = Color.white;
//				//public float AmbientIntensity { get; set; } = 1f;


//				// Option 2: light always from camera-relative angle (what you tried)
//				// _scene.MainLightDirection = _cameraWrapper.rotation * new Vector3(0.4f, -0.8f, -0.3f);

//				// Option 3: light from behind the camera (dramatic rim/key)
//				// _scene.MainLightDirection = -_cameraWrapper.rotation * Vector3.forward;
//			}



//			if (def == null || string.IsNullOrEmpty(def.model))
//			{
//				var missing = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
//				RenderMissingModelIcon(missing);
//				return missing;
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

//			// Frame the model
//			var center = modelData.bounds.center;
//			var radius = modelData.bounds.extents.magnitude * 1.2f;
//			var dist = radius / Mathf.Tan(_cameraWrapper.fieldOfView * 0.5f * Mathf.Deg2Rad);
//			_cameraWrapper.position = center - _cameraWrapper.rotation * Vector3.forward * dist;

//			// Render through the custom feature (this triggers your CommandBufferPass)
//			_cameraWrapper.Render();

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

//		private void RenderMissingModelIcon(Texture2D dst)
//		{
//			var pixels = new Color32[dst.width * dst.height];
//			var margin = dst.width / 4;
//			var thick = dst.width / 16;
//			var color = new Color32(51, 128, 255, 255); // blue-ish

//			for (var py = 0; py < dst.height; py++)
//				for (var px = 0; px < dst.width; px++)
//				{
//					var inH = px >= margin && px < dst.width - margin;
//					var inV = py >= margin && py < dst.height - margin;
//					if (!inH || !inV) continue;

//					if ((px < margin + thick) || (px >= dst.width - margin - thick && px < dst.width - margin) ||
//						(py < margin + thick) || (py >= dst.height - margin - thick && py < dst.height - margin))
//						pixels[py * dst.width + px] = color;
//				}

//			dst.SetPixels32(0, 0, dst.width, dst.height, pixels);
//		}

//		public void Dispose()
//		{
//			if (_rt != null) _rt.Release();
//			_cameraWrapper?.Destroy();
//			if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
//		}
//	}
//}