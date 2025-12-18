using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		public EditorController editorController;
		public Camera editorCamera => editorController?.editorCamera;

		protected int HitTile(Vector3 mousePos) => editorController.iMapManager.CameraHitTile(editorCamera, mousePos);

		public bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());
		public virtual bool IsMouseOverGUI() => editorController.IsMouseOverGui() | IsGuiControlActive();
		protected virtual bool IsMouseOverPreview() => false;

		public EditorControllerMovement(EditorController controller = null) => editorController = controller;

		private bool touchStartOverGui = false;
		public virtual void Update()
		{
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
				touchStartOverGui = IsMouseOverGUI() || IsMouseOverPreview();

			var allowScroll = !(IsMouseOverGUI() || IsMouseOverPreview());
			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
				allowScroll = !touchStartOverGui;
			else
				touchStartOverGui = false;

			if (!touchStartOverGui)
				EditorCameraMovement.UpdateCamera(editorCamera ? editorCamera.transform : null, isMouseOverGui: !allowScroll);
		}

		//public virtual void Start() { }//ToDo
		public virtual void OnEnable() { }
		public virtual void OnDisable() { }
		public virtual void OnGUI() { }

		public virtual void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);
	}
}