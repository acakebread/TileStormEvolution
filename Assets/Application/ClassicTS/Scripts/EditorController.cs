using UnityEngine;
using MassiveHadronLtd;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		private MapManager mapManager;
		private GameObject gridLines;
		private EditorControllerMovement activeMode;
		private EditorControllerDrag dragMode;
		private EditorControllerPaint paintMode;
		public enum EditorMode { Drag, Paint }
		private PlaceholderUI placeholderUI;
		private PlaceholderEditorUI editorUI;

		// Public getter for paintMode
		public EditorControllerPaint PaintMode => paintMode;

		private void Awake()
		{
			if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

			placeholderUI = FindAnyObjectByType<PlaceholderUI>();
			editorUI = FindAnyObjectByType<PlaceholderEditorUI>();
			if (null == placeholderUI) Debug.LogWarning("PlaceholderUI not found in scene!");
			if (null == editorUI) Debug.LogWarning("PlaceholderEditorUI not found in scene!");

			GeometryUtil.InitializeGhostMaterial();
		}

		public void Initialise(MapManager map)
		{
			Destroy();

			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Editor, true);

			mapManager = map;

			editorUI = FindAnyObjectByType<PlaceholderEditorUI>();
			if (null == editorUI)
			{
				Debug.LogError("PlaceholderEditorUI not found in scene!");
				return;
			}
			var camera = controller.activeSystem?.camera;
			editorUI.Initialize(this, mapManager, camera);

			gridLines = null != mapManager ? GridLinesHelper.CreateGridLines(transform, mapManager.Width, mapManager.Height, 0f, MapManager.tile_origin.x - 0.5f) : null;
			UpdateGridLines(editorUI.GetGridLinesEnabled() & isActiveAndEnabled);

			if (isActiveAndEnabled) OnEnable();
		}

		public void UpdateGridLines(bool value) { if (null != gridLines) gridLines.SetActive(value); }

		private void Update()
		{
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			var camera = controller.activeSystem?.camera;

			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			if (null != activeMode)
				activeMode.Update();

			if (editorUI.currentMode != (activeMode == dragMode ? EditorMode.Drag : EditorMode.Paint))
				SetMode(editorUI.currentMode);
		}

		void OnEnable()
		{
			if (null != editorUI)
			{
				editorUI.enabled = true;
				UpdateGridLines(editorUI.GetGridLinesEnabled());
			}

			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			var camera = controller.activeSystem?.camera;
			dragMode = new EditorControllerDrag(camera);
			paintMode = new EditorControllerPaint(camera, mapManager, 0);
			activeMode = editorUI.currentMode == EditorMode.Drag ? dragMode : paintMode;
			controller.UpdateGestureControllerState();
		}

		void OnDisable()
		{
			if (null != gridLines) gridLines.SetActive(false); // Only deactivate grid lines, don't reset UI state
			if (null != editorUI) editorUI.enabled = false;
		}

		void Destroy()
		{
			if (null != gridLines) Destroy(gridLines);
		}

		void OnDestroy()
		{
			Destroy();
			GeometryUtil.DestroyGhostTile();
		}

		public void OnApplicationFocus(bool hasFocus)
		{
			if (null != activeMode) activeMode.OnApplicationFocus(hasFocus);
		}

		public void SetMode(EditorMode mode)
		{
			activeMode = mode == EditorMode.Drag ? dragMode : paintMode;
		}
	}
}