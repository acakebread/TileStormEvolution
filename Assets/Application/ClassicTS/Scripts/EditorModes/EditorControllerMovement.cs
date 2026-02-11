using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		// ─── drag-to-pan
		protected bool isPanning;
		protected Vector3 panStartWorldPoint;

		protected Vector3 mouseDownPos;
		protected bool mouseMovedBeyondThreshold;
		protected const float CLICK_THRESHOLD = 8f;

		private EditorController editorController;
		protected IMapEdit iMap => editorController?.iMap;
		protected Camera camera { get { if (editorController.TryGetComponent<MainCameraController>(out var controller)) return controller.activeSystem?.camera; return null; } }

		protected bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());
		protected virtual bool IsMouseOverGUI() => editorController.IsMouseOverGui() | IsGuiControlActive();
		public virtual void OnMapLoaded() 
		{
			ViewPreviewUtil.Hide();
			isPanning = false; 
		}

		private bool touchStartOverGui = false;
		public EditorControllerMovement(EditorController controller = null) => editorController = controller;

		//public virtual void Awake() { }//not implemented yet

		public virtual void Update()
		{
			// update threshold flag
			if (Input.GetMouseButton(0) || Input.GetMouseButton(1) && Vector3.Distance(Input.mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
				mouseMovedBeyondThreshold = true;

			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				touchStartOverGui = IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview();

			var allowScroll = !(IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview());
			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
				allowScroll = !touchStartOverGui;
			else
				touchStartOverGui = false;

			ViewPreviewUtil.Update();
			if (ViewPreviewUtil.IsInFocus)
			{
				EditorCameraMovement.UpdateCamera(ViewPreviewUtil.PreviewCamera.transform);
			}
			else
			{
;				if (!touchStartOverGui)
					EditorCameraMovement.UpdateCamera(camera ? camera.transform : null, isMouseOverGui: !allowScroll);
			}

			if (ViewPreviewUtil.IsInFocus || IsMouseOverGUI() || IsGuiControlActive())
				return;

			if (MassiveHadronLtd.GuiUtils.WasGuiActiveLastFrame)
				return; // Skip input this frame — GUI consumed it last frame

			if (EditorTransformUtil.HandleTransformGizmoInput(camera))
			{
				HandleGizmoInput();
				return;
			}

			if (EditorTransformUtil.MouseOverGizmo(camera))
				return;

			OnControl();
		}

		public virtual void OnEnable() => ViewPreviewUtil.Hide();

		public virtual void OnDisable() 
		{
			ViewPreviewUtil.Hide();
			isPanning = false; 
		}

		//public virtual void OnPostRender() { }//not available in URP

		public virtual void OnGUI()  => ViewPreviewUtil.OnGUI();

		public virtual void OnDestroy() { }

		public virtual void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);

		protected virtual void HandleGizmoInput() { }

		protected virtual void OnControl() { }

		protected virtual void StartPanning()
		{
			panStartWorldPoint = Map.ScreenToWorld(camera, Input.mousePosition);
			isPanning = panStartWorldPoint != Vector3.negativeInfinity;
		}

		protected void UpdatePan()
		{
			if (!isPanning) return;

			var currentWorldPoint = Map.ScreenToWorld(camera, Input.mousePosition);
			if (currentWorldPoint != Vector3.negativeInfinity)
			{
				var delta = panStartWorldPoint - currentWorldPoint;
				camera.transform.position += delta;
			}
		}

		// Helper — can be overridden if a mode wants different pan conditions
		protected virtual bool ShouldStartPanningOnLeftClick() => true;
	}
}