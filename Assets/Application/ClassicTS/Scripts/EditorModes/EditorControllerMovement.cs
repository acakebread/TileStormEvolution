// File: EditorControllerMovement.cs
using UnityEngine;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		protected EditorController editorController;
		protected Camera editorCamera => editorController?.editorCamera;

		protected int HitTile(Vector3 mousePos) =>
			editorController.iMapManager.CameraHitTile(editorCamera, mousePos);

		public virtual bool IsMouseOverModeGui() => false;

		public EditorControllerMovement(EditorController controller = null)
		{
			editorController = controller;
		}

		public virtual void Update()
		{
			// Only move main editor camera if not overridden elsewhere
			if (editorCamera != null && ShouldUseMainCameraThisFrame())
			{
				EditorCameraMovement.UpdateCamera(editorCamera.transform);
			}
		}

		// This will be overridden in Attachment mode to detect preview interaction
		protected virtual bool ShouldUseMainCameraThisFrame() => true;

		public virtual void OnEnable() { }
		public virtual void OnDisable() { }
		public virtual void OnGui() { }

		public virtual void OnApplicationFocus(bool hasFocus)
		{
			EditorCameraMovement.OnApplicationFocus(hasFocus);
		}
	}
}