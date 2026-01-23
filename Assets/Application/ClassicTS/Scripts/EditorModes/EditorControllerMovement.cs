using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		private EditorController editorController;
		protected Camera camera { get { if (editorController.TryGetComponent<MainCameraController>(out var controller)) return controller.activeSystem?.camera; return null; } }

		protected int HitTile(Vector3 position) => iMapManager.CameraHitTile(camera, position);

		protected bool IsGuiControlActive() => GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());
		protected virtual bool IsMouseOverGUI() => editorController.IsMouseOverGui() | IsGuiControlActive();
		protected IMap currentMap => editorController?.iMapManager;
		protected IMap iMapManager => editorController?.iMapManager;
		public virtual void OnMapLoaded() { }

		public EditorControllerMovement(EditorController controller = null) => editorController = controller;

		private bool touchStartOverGui = false;
		public virtual void Update()
		{
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

		public virtual void OnEnable() { }
		public virtual void OnDisable() { }
		public virtual void OnGUI() { }
		public virtual void OnDestroy() { }

		public virtual void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);
	}
}