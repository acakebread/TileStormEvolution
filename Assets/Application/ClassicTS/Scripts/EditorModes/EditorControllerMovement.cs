using MassiveHadronLtd;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		// ─── drag-to-pan
		private bool isPanning;
		protected Vector3 beginWorld;

		protected Vector3 mouseDownPos;
		private bool mouseMovedBeyondThreshold;
		private const float CLICK_THRESHOLD = 3f;

		private readonly EditorController editorController;
		protected IMapEdit iMap => editorController?.iMap;
		protected Camera camera { get { if (editorController.TryGetComponent<MainCameraController>(out var controller)) return controller.activeSystem?.camera; return null; } }

		private bool IsMouseOverGUI() => PlaceholderUI.IsMouseOverGui() || GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) || EditorAttachmentUI.sidePanel.IsMouseOver;
		public virtual void OnMapLoaded() 
		{
			ViewPreviewUtil.Hide();
			isPanning = false; 
		}

		private bool touchStartOverGui = false;
		public EditorControllerMovement(EditorController controller = null) => editorController = controller;

		public virtual void Update()
		{
			if (InputX.GetMouseButtonDown(0) || InputX.GetMouseButtonDown(1))
			{
				mouseDownPos = InputX.mousePosition;
				mouseMovedBeyondThreshold = false;// update threshold flag
				touchStartOverGui = IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview();
			}
			
			if ((InputX.GetMouseButton(0) || InputX.GetMouseButton(1)) && Vector3.Distance(InputX.mousePosition, mouseDownPos) >= CLICK_THRESHOLD || InputX.GetAxis("Mouse ScrollWheel") > 0.01f)
				mouseMovedBeyondThreshold = true;// update threshold flag

			if (InputX.GetMouseButtonUp(0))
			{
				isPanning = false;
				GUIUtility.hotControl = 0;
			}

			var allowScroll = !(IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview());
			if (InputX.GetMouseButton(0) || InputX.GetMouseButton(1))
				allowScroll = !touchStartOverGui;
			else
			{
				if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
					Debug.Log("rogue state - problem with InputX caching");
				touchStartOverGui = false;
			}

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

			if (ViewPreviewUtil.IsInFocus || IsMouseOverGUI())
				return;

			if (GuiUtils.WasGuiActiveLastFrame)
			{
				mouseMovedBeyondThreshold = true;//workaround to suppress clean click after popup closed
				return; // Skip input this frame — GUI consumed it last frame
			}

			var handled = EditorTransformUtil.HandleTransformGizmoInput(camera);
			EditorTransformUtil.UpdateTransformGizmoVisuals(camera);
			if (handled)
			{
				HandleGizmoInput();
				return;
			}

			if (EditorTransformUtil.MouseOverGizmo(camera))
				return;

			OnControl(!mouseMovedBeyondThreshold);//static click

			if (isPanning)
				UpdatePan();
		}

		public virtual void OnEnable() => ViewPreviewUtil.Hide();

		public virtual void OnDisable() 
		{
			ViewPreviewUtil.Hide();
			isPanning = false; 
		}

		public virtual void OnGUI() => ViewPreviewUtil.OnGUI();

		public virtual void OnDestroy() { }

		public virtual void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);

		protected virtual void HandleGizmoInput() { }

		protected virtual void OnControl(bool staticClick) { }

		protected void StartPanning()
		{
			if (isPanning) return;
			beginWorld = Map.ScreenToWorld(camera, InputX.mousePosition);
			isPanning = beginWorld != Vector3.negativeInfinity;
		}

		private void UpdatePan()
		{
			var currentWorldPoint = Map.ScreenToWorld(camera, InputX.mousePosition);
			if (currentWorldPoint != Vector3.negativeInfinity)
			{
				var delta = beginWorld - currentWorldPoint;
				camera.transform.position += delta;
			}
		}
	}
}