using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class PreviewSceneController : IDisposable
	{
		// Configuration - defaults are just fallbacks, expect override
		public Vector2 DefaultResolution { get; set; } = new Vector2(320, 240);
		public Color BackgroundColor { get; set; } = new Color(0.129f, 0.698f, 0.882f); // #21B2E1
		public float FieldOfView { get; set; } = 60f;

		public Color GroundColor { get; set; } = Color.white;
		public float GroundSize { get; set; } = 2.5f;
		public float GroundY { get; set; } = -0.02f;
		public float GroundUVScale { get; set; } = 1f;
		public Texture2D GroundOverrideTexture { get; set; }

		// Runtime
		public CommandRenderCamera Camera { get; private set; }
		public CommandRenderScene Scene { get; private set; }
		public CommandRenderModelData GroundModelData { get; private set; }
		public CommandRenderModelData CurrentModelData { get; private set; }
		public RenderTexture RenderTexture { get; private set; }

		private readonly RawImage targetRawImage;
		private readonly RectTransform previewRect;
		private Vector2 lastKnownSize = Vector2.zero;
		private bool isInitialized;

		public PreviewSceneController(
			RawImage targetImage,
			RectTransform imageRectTransform)
		{
			targetRawImage = targetImage ?? throw new ArgumentNullException(nameof(targetImage));
			previewRect = imageRectTransform ?? throw new ArgumentNullException(nameof(imageRectTransform));
		}

		// ── Lazy Initialization ──────────────────────────────────────────────

		private void EnsureInitialized()
		{
			if (isInitialized) return;
			isInitialized = true;

			Camera = new CommandRenderCamera("PreviewCam", null, BackgroundColor, FieldOfView);
			Scene = new CommandRenderScene();
			Camera.AssignCommandProvider(Scene);

			CreateGroundPlane();
			UpdateRenderTextureSizeIfNeeded();
		}

		// Public API methods – all call EnsureInitialized first

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

		private void CreateGroundPlane()
		{
			var mesh = MeshUtils.GenerateQuadXZ(GroundSize, GroundUVScale, "PreviewGroundMesh");

			var tex = GroundOverrideTexture != null
				? GroundOverrideTexture
				: TextureUtils.GenerateXorTexture256();

			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				Debug.LogError("URP Unlit shader not found!");
				return;
			}

			var mat = new Material(shader)
			{
				name = "PreviewGroundMat",
				hideFlags = HideFlags.HideAndDontSave
			};

			mat.SetFloat("_Surface", 0f);
			mat.SetTexture("_BaseMap", tex);
			mat.SetColor("_BaseColor", GroundColor);

			GroundModelData = new CommandRenderModelData(
				mesh,
				new[] { mat },
				Matrix4x4.Translate(Vector3.up * GroundY));
		}

		private void UpdateActiveModels()
		{
			var active = new List<CommandRenderModelData>();

			if (GroundModelData != null)
				active.Add(GroundModelData);

			if (CurrentModelData != null)
				active.Add(CurrentModelData);

			Scene.SetModels(active.ToArray());
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

			// Ground cleanup
			if (GroundModelData != null)
			{
				foreach (var inst in GroundModelData.meshInstances)
				{
					if (inst.mesh != null && inst.mesh.name.Contains("PreviewGroundMesh"))
						UnityEngine.Object.DestroyImmediate(inst.mesh);

					if (inst.materials != null)
					{
						foreach (var m in inst.materials)
						{
							if (m != null && m.name == "PreviewGroundMat")
								UnityEngine.Object.DestroyImmediate(m);
						}
					}
				}
			}

			targetRawImage.texture = null;
		}

		public void Dispose() => Cleanup();
	}
}