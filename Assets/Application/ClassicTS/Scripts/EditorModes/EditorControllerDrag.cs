using UnityEngine;

namespace ClassicTilestorm
{
	public class EditorControllerDrag : EditorControllerMovement
	{
		private bool dragging;
		private bool isDraggingWithLeftMouse;
		private Vector3 dragStartWorldPoint;
		private Vector3 cameraStartPosition;
		private Plane dragPlane;

		public EditorControllerDrag(Camera camera) : base(camera) { }

		public override void Update()
		{
			base.Update();
			if (!camera) return;

			var wasDragging = dragging;
			var cameraTransform = camera.transform;

			// Use PlaceholderEditorUI for GUI checks
			var isGuiControlActive = PlaceholderEditorUI.Instance.IsGuiControlActive();

			// Handle mouse button down
			if (Input.GetMouseButtonDown(0) && !PlaceholderEditorUI.Instance.IsMouseOverGui())
			{
				dragging = true;
				isDraggingWithLeftMouse = true;
				cameraStartPosition = cameraTransform.position;
				var ray = camera.ScreenPointToRay(Input.mousePosition);
				if (Physics.Raycast(ray, out RaycastHit hit))
				{
					dragPlane = new Plane(Vector3.up, new Vector3(0f, hit.point.y, 0f));
					dragStartWorldPoint = hit.point;
				}
				else
				{
					dragPlane = new Plane(Vector3.up, new Vector3(0f, 0f, 0f));
					if (dragPlane.Raycast(ray, out float enter))
						dragStartWorldPoint = ray.GetPoint(enter);
					else
						dragStartWorldPoint = cameraTransform.position;
				}
			}

			// Handle plane-based dragging
			if (dragging && wasDragging && isDraggingWithLeftMouse && Input.GetMouseButton(0) && !isGuiControlActive)
			{
				cameraTransform.position = cameraStartPosition;
				Ray currentRay = camera.ScreenPointToRay(Input.mousePosition);
				if (dragPlane.Raycast(currentRay, out float enter))
				{
					var currentWorldPoint = currentRay.GetPoint(enter);
					var delta = dragStartWorldPoint - currentWorldPoint;
					cameraTransform.position += delta;
				}
			}

			// Stop dragging when mouse button is released
			if (!Input.GetMouseButton(0))
			{
				dragging = false;
				isDraggingWithLeftMouse = false;
			}
		}
	}
}