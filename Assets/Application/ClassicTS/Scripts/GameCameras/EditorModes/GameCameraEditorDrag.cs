using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class GameCameraEditorDrag : GameCameraEditorMovement
	{
		private Vector3 dragOrigin;
		private bool isDragging;

		public GameCameraEditorDrag(Camera camera) : base(camera) { }

		public override void Update()
		{
			base.Update();
			if (!camera) return;

			var cameraTransform = camera.transform;

			// Handle drag movement
			if (!GUIManager.IsMouseOverGui() && !EventSystem.current.IsPointerOverGameObject())
			{
				if (Input.GetMouseButtonDown(0) && InsideWindow())
				{
					isDragging = true;
					dragOrigin = Input.mousePosition;
					//Debug.Log("Started dragging camera");
				}
			}

			if (isDragging && Input.GetMouseButton(0))
			{
				Vector3 delta = Input.mousePosition - dragOrigin;
				cameraTransform.Translate(-delta.x * dragSpeed, -delta.y * dragSpeed, 0, Space.World);
				dragOrigin = Input.mousePosition;
			}

			if (Input.GetMouseButtonUp(0))
			{
				isDragging = false;
				//Debug.Log("Stopped dragging camera");
			}
		}
	}
}