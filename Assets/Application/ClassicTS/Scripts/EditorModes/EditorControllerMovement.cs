using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		protected EditorController editorController;
		protected Camera editorCamera => editorController?.editorCamera;

		protected int HitTile(Vector3 mousePos) => editorController.iMapManager.CameraHitTile(editorCamera, mousePos);

		public virtual bool IsMouseOverGui() => editorController.IsMouseOverGui();
		public bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		// This will be overridden in Attachment mode to detect preview interaction
		protected virtual bool ShouldUseMainCameraThisFrame() => true;

		public EditorControllerMovement(EditorController controller = null) => editorController = controller;

		public virtual void Update()
		{
			// Only move main editor camera if not overridden elsewhere
			if (editorCamera != null && ShouldUseMainCameraThisFrame()) EditorCameraMovement.UpdateCamera(editorCamera.transform);
		}

		//public virtual void Start() { }
		public virtual void OnEnable() { }
		public virtual void OnDisable() { }
		public virtual void OnGui() { }

		public virtual void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);
	}
}