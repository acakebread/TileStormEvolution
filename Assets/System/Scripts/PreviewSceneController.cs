using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq; // for convenience in one method

namespace MassiveHadronLtd
{
	public class PreviewSceneController : IDisposable
	{
		// Configuration (unchanged)
		public Vector2 DefaultResolution { get; set; } = new Vector2(320, 200);
		public Color BackgroundColor { get; set; } = new Color(0.129f, 0.698f, 0.882f); // #21B2E1
		public float FieldOfView { get; set; } = 60f;

		public Color GroundColor { get; set; } = Color.white;
		public float GroundSize { get; set; } = 2.5f;

		private float _groundY = -0.02f;
		public float GroundY
		{
			get => _groundY;
			set
			{
				if (Mathf.Approximately(_groundY, value)) return;
				_groundY = value;

				if (isInitialized)
					UpdateGroundPosition();
			}
		}

		public float GroundUVScale { get; set; } = 1f;
		public Texture2D GroundOverrideTexture { get; set; }

		// Runtime
		public CommandRenderCamera Camera { get; private set; }
		public CommandRenderScene Scene { get; private set; }
		public CommandRenderModelData GroundModelData { get; private set; }

		/// <summary>
		/// The "main" model (for backward compatibility with existing code).
		/// Use AddModel / RemoveModel for additional ones.
		/// </summary>
		public CommandRenderModelData CurrentModelData { get; private set; }

		/// <summary>
		/// Read-only view of all additional models (excluding Ground and CurrentModelData).
		/// </summary>
		public IReadOnlyList<CommandRenderModelData> AdditionalModels => _additionalModels;

		public RenderTexture RenderTexture { get; private set; }

		// Direct references for simple cleanup
		private Mesh _groundMesh;
		private Material _groundMaterial;

		private readonly RawImage targetRawImage;
		private readonly RectTransform previewRect;
		private Vector2 lastKnownSize = Vector2.zero;
		private bool isInitialized;

		// ── New: Internal list for multiple models ─────────────────────────────────
		private readonly List<CommandRenderModelData> _additionalModels = new();

		public PreviewSceneController(RawImage targetImage, RectTransform imageRectTransform)
		{
			targetRawImage = targetImage ?? throw new ArgumentNullException(nameof(targetImage));
			previewRect = imageRectTransform ?? throw new ArgumentNullException(nameof(imageRectTransform));
		}

		private void EnsureInitialized()
		{
			if (isInitialized) return;
			isInitialized = true;

			Camera = new CommandRenderCamera("PreviewCam", null, BackgroundColor, FieldOfView);
			Scene = new CommandRenderScene();
			Camera.AssignCommandProvider(Scene);

			CreateGroundPlane();
			UpdateActiveModels();

			UpdateRenderTextureSizeIfNeeded();
		}

		// ── Backward-compatible single-model API ─────────────────────────────────

		public void SetModel(CommandRenderModelData modelData)
		{
			EnsureInitialized();
			CurrentModelData = modelData;
			UpdateActiveModels();
		}

		public void ClearModel()
		{
			EnsureInitialized();
			CurrentModelData = null;
			UpdateActiveModels();
		}

		// ── New multi-model API ─────────────────────────────────────────────────

		/// <summary>
		/// Adds a model to the preview (in addition to the main CurrentModelData and Ground).
		/// </summary>
		public void AddModel(CommandRenderModelData modelData)
		{
			if (modelData == null) return;
			EnsureInitialized();

			if (!_additionalModels.Contains(modelData))
			{
				_additionalModels.Add(modelData);
				UpdateActiveModels();
			}
		}

		/// <summary>
		/// Removes a specific additional model.
		/// </summary>
		public void RemoveModel(CommandRenderModelData modelData)
		{
			if (modelData == null) return;
			EnsureInitialized();

			if (_additionalModels.Remove(modelData))
				UpdateActiveModels();
		}

		/// <summary>
		/// Clears all additional models (does NOT clear CurrentModelData or Ground).
		/// </summary>
		public void ClearAdditionalModels()
		{
			EnsureInitialized();
			if (_additionalModels.Count > 0)
			{
				_additionalModels.Clear();
				UpdateActiveModels();
			}
		}

		/// <summary>
		/// Replaces all additional models at once (convenience).
		/// </summary>
		public void SetAdditionalModels(IEnumerable<CommandRenderModelData> models)
		{
			EnsureInitialized();
			_additionalModels.Clear();
			if (models != null)
				_additionalModels.AddRange(models.Where(m => m != null));
			UpdateActiveModels();
		}

		// ── Rendering & Update (unchanged except internal call) ─────────────────

		public void UpdateAndRender()
		{
			UpdateRenderTextureSizeIfNeeded();
			Camera?.Render();
		}

		public void UpdateRenderTextureSizeIfNeeded()
		{
			EnsureInitialized();

			if (previewRect == null) return;

			Vector2 currentSize = previewRect.rect.size;
			if (currentSize.x <= 0 || currentSize.y <= 0)
				currentSize = DefaultResolution;

			if (currentSize == lastKnownSize) return;
			if (currentSize.x < 16 || currentSize.y < 16) return;

			lastKnownSize = currentSize;

			int w = Mathf.RoundToInt(currentSize.x);
			int h = Mathf.RoundToInt(currentSize.y);

			if (RenderTexture != null && RenderTexture.width == w && RenderTexture.height == h)
				return;

			if (RenderTexture != null)
			{
				RenderTexture.Release();
				RenderTexture = null;
			}

			RenderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
			{
				name = "PreviewRT"
			};
			RenderTexture.Create();

			Camera.targetTexture = RenderTexture;
			Camera.aspect = (float)w / h;

			targetRawImage.texture = RenderTexture;
		}

		public void ApplyExternalCameraTransform(Vector3 position, Quaternion rotation)
		{
			if (Camera == null) return;
			Camera.position = position;
			Camera.rotation = rotation;
		}

		// ── Ground Plane (unchanged) ────────────────────────────────────────────

		private void CreateGroundPlane()
		{
			if (GroundModelData != null) return;

			_groundMesh = MeshUtils.GenerateQuadXZ(GroundSize, GroundUVScale, "PreviewGroundMesh");

			var tex = GroundOverrideTexture != null
				? GroundOverrideTexture
				: TextureUtils.GenerateXorTexture256();

			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				Debug.LogError("URP Unlit shader not found!");
				return;
			}

			_groundMaterial = new Material(shader)
			{
				name = "PreviewGroundMat",
				hideFlags = HideFlags.HideAndDontSave
			};

			_groundMaterial.SetFloat("_Surface", 0f);
			_groundMaterial.SetTexture("_BaseMap", tex);
			_groundMaterial.SetColor("_BaseColor", GroundColor);

			GroundModelData = new CommandRenderModelData(
				_groundMesh,
				new[] { _groundMaterial },
				Matrix4x4.Translate(Vector3.up * _groundY));
		}

		private void UpdateGroundPosition()
		{
			if (GroundModelData == null || GroundModelData.meshInstances.Count == 0)
				return;

			var oldInfo = GroundModelData.meshInstances[0];

			var newInfo = new MeshInstanceInfo(
				oldInfo.mesh,
				oldInfo.materials,
				Matrix4x4.Translate(Vector3.up * _groundY),
				oldInfo.layer
			);

			GroundModelData.meshInstances[0] = newInfo;
		}

		// ── Updated to support multiple models ──────────────────────────────────

		private void UpdateActiveModels()
		{
			var active = new List<CommandRenderModelData>();

			if (GroundModelData != null)
				active.Add(GroundModelData);

			if (CurrentModelData != null)
				active.Add(CurrentModelData);

			active.AddRange(_additionalModels);

			Scene.SetModels(active.ToArray());
		}

		// Lighting methods (unchanged)
		public void SetLightToCameraDirection()
		{
			if (Scene == null || Camera == null) return;
			Scene.MainLightDirection = Camera.rotation * Vector3.back;
		}

		public void SetLighting(
			Vector3? mainLightDirection = null,
			Color? mainLightColor = null,
			float? mainLightIntensity = null,
			Color? ambientColor = null,
			float? ambientIntensity = null)
		{
			if (Scene == null) return;

			if (mainLightDirection.HasValue)
				Scene.MainLightDirection = mainLightDirection.Value;

			if (mainLightColor.HasValue)
				Scene.MainLightColor = mainLightColor.Value;

			if (mainLightIntensity.HasValue)
				Scene.MainLightIntensity = mainLightIntensity.Value;

			if (ambientColor.HasValue)
				Scene.AmbientColor = ambientColor.Value;

			if (ambientIntensity.HasValue)
				Scene.AmbientIntensity = ambientIntensity.Value;
		}

		public void Cleanup()
		{
			if (RenderTexture != null)
			{
				RenderTexture.Release();
				RenderTexture = null;
			}

			Camera?.Destroy();
			Camera = null;

			Scene?.Destroy();
			Scene = null;

			if (_groundMesh != null)
			{
				UnityEngine.Object.DestroyImmediate(_groundMesh);
				_groundMesh = null;
			}

			if (_groundMaterial != null)
			{
				UnityEngine.Object.DestroyImmediate(_groundMaterial);
				_groundMaterial = null;
			}

			GroundModelData = null;
			CurrentModelData = null;
			_additionalModels.Clear();

			targetRawImage.texture = null;
		}

		public void Dispose() => Cleanup();
	}
}

//using System;
//using UnityEngine;
//using UnityEngine.UI;
//using System.Collections.Generic;

//namespace MassiveHadronLtd
//{
//	public class PreviewSceneController : IDisposable
//	{
//		// Configuration
//		public Vector2 DefaultResolution { get; set; } = new Vector2(320, 200);
//		public Color BackgroundColor { get; set; } = new Color(0.129f, 0.698f, 0.882f); // #21B2E1
//		public float FieldOfView { get; set; } = 60f;

//		public Color GroundColor { get; set; } = Color.white;
//		public float GroundSize { get; set; } = 2.5f;

//		private float _groundY = -0.02f;
//		public float GroundY
//		{
//			get => _groundY;
//			set
//			{
//				if (Mathf.Approximately(_groundY, value)) return;
//				_groundY = value;

//				if (isInitialized)
//					UpdateGroundPosition();
//			}
//		}

//		public float GroundUVScale { get; set; } = 1f;
//		public Texture2D GroundOverrideTexture { get; set; }

//		// Runtime
//		public CommandRenderCamera Camera { get; private set; }
//		public CommandRenderScene Scene { get; private set; }
//		public CommandRenderModelData GroundModelData { get; private set; }
//		public CommandRenderModelData CurrentModelData { get; private set; }
//		public RenderTexture RenderTexture { get; private set; }

//		// Direct references for simple cleanup
//		private Mesh _groundMesh;
//		private Material _groundMaterial;

//		private readonly RawImage targetRawImage;
//		private readonly RectTransform previewRect;
//		private Vector2 lastKnownSize = Vector2.zero;
//		private bool isInitialized;

//		public PreviewSceneController(RawImage targetImage, RectTransform imageRectTransform)
//		{
//			targetRawImage = targetImage ?? throw new ArgumentNullException(nameof(targetImage));
//			previewRect = imageRectTransform ?? throw new ArgumentNullException(nameof(imageRectTransform));
//		}

//		private void EnsureInitialized()
//		{
//			if (isInitialized) return;
//			isInitialized = true;

//			Camera = new CommandRenderCamera("PreviewCam", null, BackgroundColor, FieldOfView);
//			Scene = new CommandRenderScene();
//			Camera.AssignCommandProvider(Scene);

//			CreateGroundPlane();
//			UpdateActiveModels();

//			UpdateRenderTextureSizeIfNeeded();
//		}

//		public void SetModel(CommandRenderModelData modelData)
//		{
//			EnsureInitialized();
//			CurrentModelData = modelData;
//			UpdateActiveModels();
//		}

//		public void ClearModel()
//		{
//			EnsureInitialized();
//			CurrentModelData = null;
//			UpdateActiveModels();
//		}

//		public void UpdateAndRender()
//		{
//			UpdateRenderTextureSizeIfNeeded();
//			Camera?.Render();
//		}

//		public void UpdateRenderTextureSizeIfNeeded()
//		{
//			EnsureInitialized();

//			if (previewRect == null) return;

//			Vector2 currentSize = previewRect.rect.size;
//			if (currentSize.x <= 0 || currentSize.y <= 0)
//				currentSize = DefaultResolution;

//			if (currentSize == lastKnownSize) return;
//			if (currentSize.x < 16 || currentSize.y < 16) return;

//			lastKnownSize = currentSize;

//			int w = Mathf.RoundToInt(currentSize.x);
//			int h = Mathf.RoundToInt(currentSize.y);

//			if (RenderTexture != null && RenderTexture.width == w && RenderTexture.height == h)
//				return;

//			if (RenderTexture != null)
//			{
//				RenderTexture.Release();
//				RenderTexture = null;
//			}

//			RenderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
//			{
//				name = "PreviewRT"
//			};
//			RenderTexture.Create();

//			Camera.targetTexture = RenderTexture;
//			Camera.aspect = (float)w / h;

//			targetRawImage.texture = RenderTexture;
//		}

//		// Restored method
//		public void ApplyExternalCameraTransform(Vector3 position, Quaternion rotation)
//		{
//			if (Camera == null) return;
//			Camera.position = position;
//			Camera.rotation = rotation;
//		}

//		// ── Ground Plane ─────────────────────────────────────────────────────

//		private void CreateGroundPlane()
//		{
//			if (GroundModelData != null) return;

//			_groundMesh = MeshUtils.GenerateQuadXZ(GroundSize, GroundUVScale, "PreviewGroundMesh");

//			var tex = GroundOverrideTexture != null
//				? GroundOverrideTexture
//				: TextureUtils.GenerateXorTexture256();

//			var shader = Shader.Find("Universal Render Pipeline/Unlit");
//			if (shader == null)
//			{
//				Debug.LogError("URP Unlit shader not found!");
//				return;
//			}

//			_groundMaterial = new Material(shader)
//			{
//				name = "PreviewGroundMat",
//				hideFlags = HideFlags.HideAndDontSave
//			};

//			_groundMaterial.SetFloat("_Surface", 0f);
//			_groundMaterial.SetTexture("_BaseMap", tex);
//			_groundMaterial.SetColor("_BaseColor", GroundColor);

//			GroundModelData = new CommandRenderModelData(
//				_groundMesh,
//				new[] { _groundMaterial },
//				Matrix4x4.Translate(Vector3.up * _groundY));
//		}

//		private void UpdateGroundPosition()
//		{
//			if (GroundModelData == null || GroundModelData.meshInstances.Count == 0)
//				return;

//			var oldInfo = GroundModelData.meshInstances[0];

//			var newInfo = new MeshInstanceInfo(
//				oldInfo.mesh,
//				oldInfo.materials,
//				Matrix4x4.Translate(Vector3.up * _groundY),
//				oldInfo.layer
//			);

//			GroundModelData.meshInstances[0] = newInfo;
//		}

//		private void UpdateActiveModels()
//		{
//			var active = new List<CommandRenderModelData>();

//			if (GroundModelData != null)
//				active.Add(GroundModelData);

//			if (CurrentModelData != null)
//				active.Add(CurrentModelData);

//			Scene.SetModels(active.ToArray());
//		}

//		// Call this to make the light come from the camera's current view direction
//		public void SetLightToCameraDirection()
//		{
//			if (Scene == null || Camera == null) return;
//			Scene.MainLightDirection = Camera.rotation * Vector3.back;   // light shines toward the model from camera
//		}

//		// Full control - call this whenever you want to change lighting
//		public void SetLighting(
//			Vector3? mainLightDirection = null,
//			Color? mainLightColor = null,
//			float? mainLightIntensity = null,
//			Color? ambientColor = null,
//			float? ambientIntensity = null)
//		{
//			if (Scene == null) return;

//			if (mainLightDirection.HasValue)
//				Scene.MainLightDirection = mainLightDirection.Value;

//			if (mainLightColor.HasValue)
//				Scene.MainLightColor = mainLightColor.Value;

//			if (mainLightIntensity.HasValue)
//				Scene.MainLightIntensity = mainLightIntensity.Value;

//			if (ambientColor.HasValue)
//				Scene.AmbientColor = ambientColor.Value;

//			if (ambientIntensity.HasValue)
//				Scene.AmbientIntensity = ambientIntensity.Value;
//		}

//		public void Cleanup()
//		{
//			if (RenderTexture != null)
//			{
//				RenderTexture.Release();
//				RenderTexture = null;
//			}

//			Camera?.Destroy();
//			Camera = null;

//			Scene?.Destroy();
//			Scene = null;

//			// Simple direct cleanup
//			if (_groundMesh != null)
//			{
//				UnityEngine.Object.DestroyImmediate(_groundMesh);
//				_groundMesh = null;
//			}

//			if (_groundMaterial != null)
//			{
//				UnityEngine.Object.DestroyImmediate(_groundMaterial);
//				_groundMaterial = null;
//			}

//			GroundModelData = null;
//			targetRawImage.texture = null;
//		}

//		public void Dispose() => Cleanup();
//	}
//}