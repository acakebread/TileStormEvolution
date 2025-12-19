using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		public EditorController editorController;
		public Camera camera { get { if (editorController.TryGetComponent<MainCameraController>(out var controller)) return controller.activeSystem?.camera; return null; } }

		protected int HitTile(Vector3 mousePos) => editorController.iMapManager.CameraHitTile(camera, mousePos);

		public bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());
		public virtual bool IsMouseOverGUI() => editorController.IsMouseOverGui() | IsGuiControlActive();
		protected virtual bool IsMouseOverPreview() => false;
		public Map currentMap => editorController?.iMapManager?.CurrentMap;
		public IMapManager iMapManager => editorController?.iMapManager;
		public virtual void OnMapChanged() { }

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
				EditorCameraMovement.UpdateCamera(camera ? camera.transform : null, isMouseOverGui: !allowScroll);
		}

		protected bool enabled = false;
		//public virtual void Start() { }//ToDo
		public virtual void OnEnable() => enabled = true;
		public virtual void OnDisable() => enabled = false;
		public virtual void OnGUI() { }
		public virtual void OnDestroy() { }

		public virtual void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);
	}
}