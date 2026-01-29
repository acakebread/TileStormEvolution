using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class MapPreviewUtil
	{
		private static GameObject root;
		private static GameObject camGO;
		private static Camera previewCam;
		private static RenderTexture renderTexture;
		private static Mesh groundMesh;
		private static Material groundMat;
		private static Texture2D groundTex;

		public static Camera PreviewCamera => previewCam;
		public static RenderTexture PreviewRenderTexture => renderTexture;
		public static Transform PreviewCameraTransform => previewCam ? previewCam.transform : null;

		public static GameObject previewRoot;
		public static Transform previewMapRoot;
		public static int previewLayer = -1;//1 << LayerMask.NameToLayer(PREVIEW_LAYER_NAME);// - 1; // cache
		public const string PREVIEW_LAYER_NAME = "Preview";

		public static void SetPreviewLayer(int layer)
		{
			previewLayer = layer;
		}

		private static void EnsurePreviewRoot()
		{
			if (previewRoot != null) return;

			previewRoot = new GameObject("PreviewSceneRoot");// { hideFlags = HideFlags.HideAndDontSave };
			previewRoot.transform.SetParent(root.transform); // under MAP_PREVIEW_ROOT
			previewMapRoot = new GameObject("MapCopy").transform;
			previewMapRoot.SetParent(previewRoot.transform);
			previewMapRoot.localPosition = Vector3.zero;
		}

		// Call this when changing map or panel disable
		public static void ClearPreviewMap()
		{
			if (previewMapRoot != null)
			{
				foreach (Transform child in previewMapRoot)
					if (child != null) Object.DestroyImmediate(child.gameObject);

				// Optional: destroy root if empty, but usually keep it
			}
		}

		public static void Initialize()
		{
			if (root != null) return;

			root = new GameObject("MAP_PREVIEW_ROOT");
			//root.hideFlags = HideFlags.HideAndDontSave;
			// Optional: UnityEngine.Object.DontDestroyOnLoad(root);

			int previewLayerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);

			if (previewLayerIndex < 0)
			{
				Debug.LogError($"Layer '{PREVIEW_LAYER_NAME}' does not exist in Project Settings → Tags and Layers!");
				previewLayerIndex = 0; // fallback – everything
			}

			// Also store the index for later use
			previewLayer = 1 << LayerMask.NameToLayer(PREVIEW_LAYER_NAME); //1 << previewLayerIndex;

			// Camera setup
			camGO = new GameObject("PreviewCamera");
			camGO.transform.SetParent(root.transform);

			previewCam = camGO.AddComponent<Camera>();
			previewCam.enabled = false;
			previewCam.clearFlags = CameraClearFlags.SolidColor;
			previewCam.backgroundColor = new Color(0.1f, 0.1f, 0.14f, 1f);
			//previewCam.cullingMask = 0;                    // we'll draw ground manually
			previewCam.cullingMask = 1 << LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
			previewCam.nearClipPlane = 0.1f;
			previewCam.farClipPlane = 2000f;

			// Reasonable default size – can be resized later if needed
			renderTexture = new RenderTexture(512, 320, 24, RenderTextureFormat.ARGB32);
			renderTexture.filterMode = FilterMode.Bilinear;
			renderTexture.Create();
			previewCam.targetTexture = renderTexture;

			CreateGroundPlane();

			EnsurePreviewRoot();
		}

		private static void CreateGroundPlane()
		{
			// Large flat quad under the camera
			groundMesh = new Mesh
			{
				vertices = new[]
				{
				new Vector3(-1000f, -0.02f, -1000f),
				new Vector3(-1000f, -0.02f,  1000f),
				new Vector3( 1000f, -0.02f,  1000f),
				new Vector3( 1000f, -0.02f, -1000f),
			},
				triangles = new[] { 0, 1, 2, 0, 2, 3 },
				uv = new[]
				{
				new Vector2(0,   0),
				new Vector2(0,  64),
				new Vector2(64, 64),
				new Vector2(64,  0),
			}
			};
			groundMesh.RecalculateNormals();
			groundMesh.hideFlags = HideFlags.HideAndDontSave;

			// Very simple seamless noise texture
			groundTex = TextureUtils.GenerateSeamlessValueNoise(256, 0.28f, 0.42f);
			groundTex.hideFlags = HideFlags.HideAndDontSave;

			// URP Unlit material
			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				Debug.LogError("MapPreviewUtil: Could not find URP/Unlit shader");
				return;
			}

			groundMat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			groundMat.SetFloat("_Surface", 0f);           // Opaque
			groundMat.SetTexture("_BaseMap", groundTex);
			groundMat.SetColor("_BaseColor", Color.white * 0.92f);

			// Add command buffer hook
			var provider = camGO.AddComponent<CameraCommandProvider>();
			provider.RegisterCommand(
				RenderPassEvent.BeforeRenderingOpaques,
				(cmd, cam) =>
				{
					if (groundMesh == null || groundMat == null) return;

					// Draw at world origin – camera position doesn't affect it
					Matrix4x4 matrix = Matrix4x4.identity;
					groundMat.SetPass(0);
					cmd.DrawMesh(groundMesh, matrix, groundMat, 0, 0);
				});
		}

		/// <summary>
		/// Call this when you want to position & render the preview for a specific location/rotation/fov
		/// </summary>
		public static void RenderPreview(Vector3 worldPosition, Quaternion rotation, float fov = 60f)
		{
			if (previewCam == null)
			{
				Initialize();
				if (previewCam == null) return;
			}

			previewCam.transform.position = worldPosition;
			previewCam.transform.rotation = rotation;
			previewCam.fieldOfView = fov;

			previewCam.Render();
		}

		public static void Cleanup()
		{
			if (renderTexture != null && renderTexture.IsCreated())
			{
				renderTexture.Release();
				renderTexture = null;
			}

			if (groundMesh) Object.DestroyImmediate(groundMesh);
			if (groundMat) Object.DestroyImmediate(groundMat);
			if (groundTex) Object.DestroyImmediate(groundTex);
			if (root) Object.DestroyImmediate(root);

			root = null;
			camGO = null;
			previewCam = null;
			groundMesh = null;
			groundMat = null;
			groundTex = null;
		}

		// ──────────────────────────────────────────────────────────────
		//  Minimal command buffer helper (kept internal)
		// ──────────────────────────────────────────────────────────────
		private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
		{
			private System.Collections.Generic.Dictionary<RenderPassEvent, System.Action<RasterCommandBuffer, Camera>> commands = new();

			public void RegisterCommand(RenderPassEvent evt, System.Action<RasterCommandBuffer, Camera> action)
			{
				commands[evt] = action;
			}

			public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

			public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
			{
				if (commands.TryGetValue(evt, out var action) && action != null)
				{
					try
					{
						action.Invoke(commandBuffer, camera);
					}
					catch (System.Exception e)
					{
						Debug.LogError($"MapPreviewUtil command error: {e}");
					}
				}
			}

			private void OnDestroy() => commands.Clear();
		}
	}

	public static class MapPreviewExtensions
	{
		public static GameObject InstantiatePreviewCopy(this Map map, Transform parent, int layer)
		{
			return map?.BuildPreviewGeometry(parent, layer);
		}
	}
}