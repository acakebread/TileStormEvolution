using UnityEngine;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		protected float yaw;
		protected float pitch;
		protected float lookSpeedH = 2f;
		protected float lookSpeedV = 2f;
		protected float zoomSpeed = 12f;
		protected bool skipNextScroll;
		protected bool didGainFocus;

		protected EditorController editorController;

		protected Camera camera 
		{
			get
			{
				if (editorController.TryGetComponent<MainCameraController>(out var controller)) return controller.activeSystem?.camera;
				return null;
			}
		}

		public EditorControllerMovement(EditorController controller = null)
		{
			editorController = controller;
			skipNextScroll = didGainFocus = false;
		}

		public virtual void Update()
		{
			if (null == camera) return;
			var cameraTransform = camera.transform;

			var ui = editorController?.GetEditorUI();
			bool isGuiActive = ui?.IsGuiControlActive() ?? false;
			bool isMouseInside = ui?.IsMouseInsideWindow() ?? true;
			bool isMouseOverGui = ui?.IsMouseOverGui() ?? false;

			if ((Input.GetMouseButton(1) || Input.touchCount > 0) && !isGuiActive && !didGainFocus)
			{
				var pointerX = Input.GetAxis("Mouse X");
				var pointerY = Input.GetAxis("Mouse Y");
				if (Input.touchCount > 0)
				{
					pointerX = Input.touches[0].deltaPosition.x * 0.05f;
					pointerY = Input.touches[0].deltaPosition.y * 0.05f;
				}
				yaw += lookSpeedH * pointerX;
				pitch -= lookSpeedV * pointerY;
				cameraTransform.eulerAngles = new Vector3(pitch, yaw, 0f);
			}

			if (isMouseInside && !isMouseOverGui)
			{
				var scroll = skipNextScroll ? 0f : Input.GetAxis("Mouse ScrollWheel");
				cameraTransform.Translate(0, 0, scroll * zoomSpeed, Space.Self);
				skipNextScroll = false;
			}

			var translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;
			cameraTransform.Translate(translation, Space.Self);

			if (isMouseInside && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
			{
				yaw = camera.transform.eulerAngles.y;
				pitch = camera.transform.eulerAngles.x; 
				didGainFocus = false;
			}
		}

		public virtual void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus) skipNextScroll = true;
			if (hasFocus) didGainFocus = true;
		}

		protected Vector3 GetInputTranslationDirection()
		{
			Vector3 direction = Vector3.zero;
			if (Input.GetKey(KeyCode.W)) direction += Vector3.forward;
			if (Input.GetKey(KeyCode.S)) direction += Vector3.back;
			if (Input.GetKey(KeyCode.A)) direction += Vector3.left;
			if (Input.GetKey(KeyCode.D)) direction += Vector3.right;
			if (Input.GetKey(KeyCode.Q)) direction += Vector3.down;
			if (Input.GetKey(KeyCode.E)) direction += Vector3.up;

			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) direction *= zoomSpeed * 0.5f;
			return direction;
		}
	}
}