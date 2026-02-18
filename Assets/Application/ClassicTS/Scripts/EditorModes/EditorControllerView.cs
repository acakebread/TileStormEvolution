using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public class EditorControllerView : EditorControllerMovement
	{
		private bool dragging;
		private bool isDraggingWithLeftMouse;
		private Vector3 dragStartWorldPoint;
		private Plane dragPlane;

		public EditorControllerView(EditorController editorController) : base(editorController) { }

		public override void Update()
		{
			base.Update();
			if (!camera) return;

			var wasDragging = dragging;
			var cameraTransform = camera.transform;

			if (InputX.GetMouseButtonDown(0) && !IsMouseOverGUI())
			{
				dragging = true;
				isDraggingWithLeftMouse = true;
				var ray = camera.ScreenPointToRay(InputX.mousePosition);
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

			if (dragging && wasDragging && isDraggingWithLeftMouse && InputX.GetMouseButton(0) && !IsGuiControlActive())
			{
				var currentRay = camera.ScreenPointToRay(InputX.mousePosition);
				if (dragPlane.Raycast(currentRay, out float enter))
				{
					var currentWorldPoint = currentRay.GetPoint(enter);
					var delta = dragStartWorldPoint - currentWorldPoint;
					cameraTransform.position += delta;
				}
			}

			if (!InputX.GetMouseButton(0))
			{
				dragging = false;
				isDraggingWithLeftMouse = false;
			}
		}
	}
}