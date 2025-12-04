using UnityEngine;

namespace ClassicTilestorm
{
	public abstract class EditorControllerMovement
	{
		private float lookSpeedH = 2f;
		private float lookSpeedV = 2f;
		private float zoomSpeed = 12f;
		private bool skipNextScroll;
		private bool didGainFocus;

		protected EditorController editorController;

		protected Camera camera
		{
			get
			{
				if (editorController.TryGetComponent<MainCameraController>(out var controller))
					return controller.activeSystem?.camera;
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
			if (camera == null) return;
			var cameraTransform = camera.transform;

			var ui = editorController?.GetEditorUI();
			bool isGuiActive = ui?.IsGuiControlActive() ?? false;
			bool isMouseOverGui = ui?.IsMouseOverGui() ?? false;

			// Right-click or touch drag → rotate camera
			if ((Input.GetMouseButton(1) || Input.touchCount > 0) && !isGuiActive && !didGainFocus)
			{
				var pointerX = Input.GetAxis("Mouse X");
				var pointerY = Input.GetAxis("Mouse Y");
				if (Input.touchCount > 0)
				{
					pointerX = Input.touches[0].deltaPosition.x * 0.05f;
					pointerY = Input.touches[0].deltaPosition.y * 0.05f;
				}

				var eulers = cameraTransform.eulerAngles;
				eulers.y += lookSpeedH * pointerX;
				eulers.x -= lookSpeedV * pointerY;
				cameraTransform.eulerAngles = eulers;
			}

			// Mouse wheel zoom — only when not over GUI
			if (!isMouseOverGui && !isGuiActive)
			{
				var scroll = skipNextScroll ? 0f : Input.GetAxis("Mouse ScrollWheel");
				if (scroll != 0f) cameraTransform.Translate(0, 0, scroll * zoomSpeed, Space.Self);
				skipNextScroll = false;
			}

			// WASD movement
			var translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;
			cameraTransform.Translate(translation, Space.Self);

			if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
				didGainFocus = false;
		}

		public virtual void OnEnable() { }
		public virtual void OnDisable() { }

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

			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
				direction *= 2f; // faster movement with shift

			return direction;
		}
	}
}