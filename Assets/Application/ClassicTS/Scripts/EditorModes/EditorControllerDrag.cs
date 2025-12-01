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

		public EditorControllerDrag(Camera camera, EditorController editorController)
			: base(camera, editorController)  // passes to base
		{
		}

		public override void Update()
		{
			base.Update();
			if (!camera) return;

			var wasDragging = dragging;
			var cameraTransform = camera.transform;

			var ui = editorController.GetEditorUI();
			var isGuiControlActive = ui.IsGuiControlActive();

			if (Input.GetMouseButtonDown(0) && !ui.IsMouseOverGui())
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

			if (dragging && wasDragging && isDraggingWithLeftMouse && Input.GetMouseButton(0) && !isGuiControlActive)
			{
				cameraTransform.position = cameraStartPosition;
				var currentRay = camera.ScreenPointToRay(Input.mousePosition);
				if (dragPlane.Raycast(currentRay, out float enter))
				{
					var currentWorldPoint = currentRay.GetPoint(enter);
					var delta = dragStartWorldPoint - currentWorldPoint;
					cameraTransform.position += delta;
				}
			}

			if (!Input.GetMouseButton(0))
			{
				dragging = false;
				isDraggingWithLeftMouse = false;
			}
		}
	}
}