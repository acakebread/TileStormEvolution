using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		protected EditorController editorController;
		protected Camera editorCamera => editorController?.editorCamera;

		protected int HitTile(Vector3 mousePos) => editorController.iMapManager.CameraHitTile(editorCamera, mousePos);

		public virtual bool IsMouseOverGUI() => editorController.IsMouseOverGui() | IsGuiControlActive();
		public bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		public EditorControllerMovement(EditorController controller = null) => editorController = controller;

		public virtual void Update()
		{
			//if (!IsMouseOverGUI())
				EditorCameraMovement.UpdateCamera(editorCamera ? editorCamera.transform : null, isMouseOverGui : IsMouseOverGUI());
		}

		//public virtual void Start() { }//ToDo
		public virtual void OnEnable() { }
		public virtual void OnDisable() { }
		public virtual void OnGUI() { }

		public virtual void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);
	}
}