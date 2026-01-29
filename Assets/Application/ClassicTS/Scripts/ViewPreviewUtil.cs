using MassiveHadronLtd;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClassicTilestorm
{
	public static class ViewPreviewUtil
	{
		private static GameObject root;
		private static GameObject camGO;
		private static Camera previewCam;
		private static RenderTexture renderTexture;
		private static Mesh groundMesh;
		private static Material groundMat;
		private static Texture2D groundTex;

		private const float PREVIEW_HEIGHT = 200f;
		private const float MARGIN = 10f;

		private static View currentView;
		private static IMapEdit currentManager;
		private static bool isVisible = false;
		private static bool isHighlighted = false;
		private static bool isInFocus = false;
		private static bool isInUse = false;

		public static Rect PreviewRect { get; private set; }

		public static bool IsInFocus => isInFocus;
		public static Camera PreviewCamera => previewCam;
		public static Transform PreviewCameraTransform => previewCam != null ? previewCam.transform : null;

		public static void Show(View view, IMapEdit manager)
		{
			if (view == null || manager == null)
			{
				Hide();
				return;
			}

			currentView = view;
			currentManager = manager;
			isVisible = true;

			EnsureCreated();
			UpdatePreviewCamera();
		}

		public static void Hide()
		{
			currentView = null;
			currentManager = null;
			isVisible = false;
			isHighlighted = false;
			isInFocus = false;
			isInUse = false;
		}

		public static void Update()
		{
			var isMoseOverPreview = IsMouseOverPreview();
			isHighlighted = isMoseOverPreview;

			if (Input.GetMouseButtonDown(1))
			{
				isInFocus = isMoseOverPreview;
				isInUse = isMoseOverPreview;
			}

			if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
			{
				isInFocus = isMoseOverPreview;
				isInUse = false;
			}

			UpdatePreviewCamera();
		}

		public static void OnGUI()
		{
			if (!isVisible || renderTexture == null || currentView == null)
				return;

			float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
			float previewWidth = PREVIEW_HEIGHT * aspect;

			PreviewRect = new Rect(
				MARGIN,
				Screen.height - PREVIEW_HEIGHT - MARGIN,
				previewWidth,
				PREVIEW_HEIGHT
			);

			GUI.Box(new Rect(PreviewRect.x - 2, PreviewRect.y - 2, PreviewRect.width + 4, PreviewRect.height + 4), "");
			GUI.DrawTexture(PreviewRect, renderTexture, ScaleMode.StretchToFill, false);
			GUI.Label(new Rect(PreviewRect.x + 8, PreviewRect.y + 8, PreviewRect.width - 16, 20), "Camera Preview");

			// Visual feedback
			if (isInUse || isHighlighted)
			{
				Color border = isInUse
					? new Color(0.3f, 0.9f, 1f, 1f)
					: new Color(0.7f, 0.9f, 1f, 0.7f);
				float t = isInUse ? 3.5f : 1.8f;

				var hitRect = PreviewRect;

				GUI.color = border;
				GUI.DrawTexture(new Rect(hitRect.x - t, hitRect.y - t, hitRect.width + t * 2, t), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(hitRect.x - t, hitRect.yMax, hitRect.width + t * 2, t), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(hitRect.x - t, hitRect.y, t, hitRect.height), Texture2D.whiteTexture);
				GUI.DrawTexture(new Rect(hitRect.xMax, hitRect.y, t, hitRect.height), Texture2D.whiteTexture);
				GUI.color = Color.white;

				if (isInUse)
				{
					GUI.color = Color.black;
					GUI.Label(new Rect(hitRect.xMax - 179, hitRect.y - 29, 170, 22), " Preview Active");
					GUI.color = new Color(0.3f, 0.9f, 1f);
					GUI.Label(new Rect(hitRect.xMax - 180, hitRect.y - 30, 170, 22), " Preview Active");
					GUI.color = Color.white;
				}
			}
		}

		public static bool IsMouseOverPreview()
		{
			if (!isVisible || PreviewRect.width <= 0) return false;
			Rect hitRect = new Rect(PreviewRect.x - 8, PreviewRect.y - 8, PreviewRect.width + 16, PreviewRect.height + 16);
			Vector2 mp = Input.mousePosition;
			mp.y = Screen.height - mp.y;
			return hitRect.Contains(mp);
		}

		private static void UpdatePreviewCamera()
		{
			if (previewCam == null || currentView == null || currentManager == null) return;

			Vector3 worldPos = currentManager.TileWorldPosition(currentView.tile) + currentView.Position;
			previewCam.transform.position = worldPos;
			previewCam.transform.rotation = currentView.Rotation;
			previewCam.fieldOfView = currentView.FOV;
			previewCam.nearClipPlane = 0.1f;
			previewCam.farClipPlane = 999f;
			previewCam.Render();
		}


		private static void EnsureCreated()
		{
			if (root != null) return;

			root = new GameObject("GIZMO_VIEWPREVIEW");
			//root.hideFlags = HideFlags.HideAndDontSave;
			UnityEngine.Object.DontDestroyOnLoad(root);

			camGO = new GameObject("PreviewCamera");        // ← assign to static field
			camGO.transform.SetParent(root.transform);
			previewCam = camGO.AddComponent<Camera>();
			previewCam.enabled = false;
			previewCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

			renderTexture = new RenderTexture(320, 200, 24, RenderTextureFormat.ARGB32);
			renderTexture.Create();
			previewCam.targetTexture = renderTexture;

			CreateGroundPlane();
		}

		private static void CreateGroundPlane()
		{
			// Ground mesh
			groundMesh = new Mesh();
			groundMesh.vertices = new Vector3[]
			{
			new Vector3(-512, -0.2f, -512),
			new Vector3(-512, -0.2f,  512),
			new Vector3( 512, -0.2f,  512),
			new Vector3( 512, -0.2f, -512)
			};
			groundMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
			groundMesh.uv = new Vector2[] { new(0, 0), new(0, 16), new(16, 16), new(16, 0) };
			groundMesh.RecalculateNormals();

			// Noise texture
			groundTex = TextureUtils.GenerateSeamlessValueNoise(256, 0.25f, 0.35f);

			// URP Unlit material
			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			groundMat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave,
			};
			groundMat.SetFloat("_Surface", 0f);
			groundMat.SetTexture("_BaseMap", groundTex);
			groundMat.SetColor("_BaseColor", Color.white);

			// Custom render command — now camGO is accessible
			var provider = camGO.AddComponent<CameraCommandProvider>();
			provider.RegisterCommand(
				RenderPassEvent.BeforeRenderingOpaques,
				(cmd, cam) =>
				{
					if (groundMesh == null || groundMat == null) return;

					Matrix4x4 matrix = Matrix4x4.identity;
					groundMat.SetPass(0);
					cmd.DrawMesh(groundMesh, matrix, groundMat, 0, 0);
				});
		}

		public static void Cleanup()
		{
			if (renderTexture != null && renderTexture.IsCreated())
				renderTexture.Release();

			if (groundMesh != null) UnityEngine.Object.DestroyImmediate(groundMesh);
			if (groundMat != null) UnityEngine.Object.DestroyImmediate(groundMat);
			if (groundTex != null) UnityEngine.Object.DestroyImmediate(groundTex);
			if (root != null) UnityEngine.Object.DestroyImmediate(root);

			root = null;
			camGO = null;
			previewCam = null;
			renderTexture = null;
			groundMesh = null;
			groundMat = null;
			groundTex = null;
		}

		// Helper class (keep internal)
		private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
		{
			private readonly System.Collections.Generic.Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

			public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

			public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

			public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
			{
				if (commands.TryGetValue(evt, out var cmd) && cmd != null)
				{
					try { cmd.Invoke(commandBuffer, camera); }
					catch (Exception e) { Debug.LogError($"ViewPreviewUtil: Error executing command: {e}"); }
				}
			}

			void OnDestroy() => commands.Clear();
		}
	}
}