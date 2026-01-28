using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		// ─── drag-to-pan
		protected bool isPanning;
		protected Vector3 panStartWorldPoint;
		protected Plane panPlane = new Plane(Vector3.up, Vector3.zero);

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

		public EditorControllerMovement(EditorController controller = null) => editorController = controller;

		private bool touchStartOverGui = false;
		public virtual void Update()
		{
			ViewPreviewUtil.Update();

			if (ViewPreviewUtil.IsInFocus)
				EditorCameraMovement.UpdateCamera(ViewPreviewUtil.PreviewCamera.transform);

			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0)
				GUIUtility.hotControl = 0;

			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				touchStartOverGui = IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview();

			var allowScroll = !(IsMouseOverGUI() || ViewPreviewUtil.IsMouseOverPreview());
			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
				allowScroll = !touchStartOverGui;
			else
				touchStartOverGui = false;

			if (!touchStartOverGui)
				EditorCameraMovement.UpdateCamera(camera ? camera.transform : null, isMouseOverGui: !allowScroll);
		}

		public virtual void OnEnable() 
		{
			ViewPreviewUtil.Hide();
		}

		public virtual void OnDisable() 
		{
			ViewPreviewUtil.Hide();
			isPanning = false; 
		}

		public virtual void OnGUI() { }
		public virtual void OnDestroy() { }

		public virtual void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);

		// You can keep this one protected virtual if derived classes want to override the plane logic
		protected virtual void StartPanning()
		{
			isPanning = true;

			var ray = camera.ScreenPointToRay(Input.mousePosition);
			if (Physics.Raycast(ray, out RaycastHit hit))
			{
				// Try to pan on the actual ground height
				panPlane = new Plane(Vector3.up, new Vector3(0, hit.point.y, 0));
				panStartWorldPoint = hit.point;
			}
			else
			{
				// Fallback — usually at y=0
				panPlane = new Plane(Vector3.up, Vector3.zero);
				panPlane.Raycast(ray, out float enter);
				panStartWorldPoint = ray.GetPoint(enter);
			}
		}

		protected void UpdatePan()
		{
			if (!isPanning) return;

			var currentRay = camera.ScreenPointToRay(Input.mousePosition);
			if (panPlane.Raycast(currentRay, out float enter))
			{
				var currentWorldPoint = currentRay.GetPoint(enter);
				var delta = panStartWorldPoint - currentWorldPoint;
				camera.transform.position += delta;
			}
		}

		// Helper — can be overridden if a mode wants different pan conditions
		protected virtual bool ShouldStartPanningOnLeftClick()
		{
			return true;
		}
	}
}