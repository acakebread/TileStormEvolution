// ViewPreview.cs — FINAL, PERFECT, YOURS + FIXED
using UnityEngine;

namespace ClassicTilestorm
{
	public class ViewPreview : MonoBehaviour
	{
		private Camera previewCam;
		private GameObject camGO;
		private RenderTexture renderTexture;
		private Rect previewRect;

		private const float PREVIEW_HEIGHT = 200f;
		private const float MARGIN = 10f;

		private View currentView;
		private IMapManager mapManager;

		public static ViewPreview Create()
		{
			var go = new GameObject("ViewPreview");
			DontDestroyOnLoad(go);
			return go.AddComponent<ViewPreview>();
		}

		private void Awake()
		{
			camGO = new GameObject("PreviewCamera");
			camGO.transform.SetParent(transform);
			previewCam = camGO.AddComponent<Camera>();
			previewCam.enabled = false;
			//previewCam.clearFlags = CameraClearFlags.SolidColor;
			//previewCam.backgroundColor = new Color(0.15f, 0.15f, 0.15f); // Dark grey

			// Hide editor gizmos
			previewCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

			renderTexture = new RenderTexture(320, 200, 24, RenderTextureFormat.ARGB32);
			renderTexture.Create();
			previewCam.targetTexture = renderTexture;

			gameObject.hideFlags = HideFlags.HideAndDontSave;
		}

		public void Show(View view, IMapManager manager)
		{
			currentView = view;
			mapManager = manager;
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			currentView = null;
			mapManager = null;
			gameObject.SetActive(false);
		}

		private void LateUpdate()
		{
			if (currentView == null || mapManager == null || !gameObject.activeSelf) return;

			Vector3 worldPos = mapManager.TileWorldPosition(currentView.tile) + currentView.Position;
			previewCam.transform.position = worldPos;
			previewCam.transform.rotation = currentView.Rotation;

			previewCam.fieldOfView = View.FOV; // As you want — will be dynamic later

			previewCam.nearClipPlane = 0.1f;
			previewCam.farClipPlane = currentView.Distance > 0 ? currentView.Distance + 10f : 200f;

			previewCam.Render();
		}

		private void OnGUI()
		{
			if (!gameObject.activeSelf || renderTexture == null || currentView == null) return;

			float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
			float previewWidth = PREVIEW_HEIGHT * aspect;

			previewRect = new Rect(
				MARGIN,
				Screen.height - PREVIEW_HEIGHT - MARGIN,
				previewWidth,
				PREVIEW_HEIGHT
			);

			GUI.Box(new Rect(previewRect.x - 2, previewRect.y - 2, previewRect.width + 4, previewRect.height + 4), "");
			GUI.DrawTexture(previewRect, renderTexture, ScaleMode.StretchToFill, false);
			GUI.Label(new Rect(previewRect.x + 8, previewRect.y + 8, previewRect.width - 16, 20), "Camera Preview", GUI.skin.name);
		}

		private void OnDestroy()
		{
			if (renderTexture != null && renderTexture.IsCreated())
				renderTexture.Release();
		}
	}
}