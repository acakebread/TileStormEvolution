using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public abstract class GameCameraEditorMovement
	{
		protected Camera camera;
		protected static float yaw; // Static to share across instances
		protected static float pitch; // Static to share across instances
		protected float lookSpeedH = 2f;
		protected float lookSpeedV = 2f;
		protected float zoomSpeed = 12f;
		protected float dragSpeed = 0.01f; // Added for drag movement
		protected bool skipNextScroll;

		public GameCameraEditorMovement(Camera camera)
		{
			this.camera = camera;
			if (camera != null && yaw == 0f && pitch == 0f) // Initialize only if not set
			{
				var cameraTransform = camera.transform;
				yaw = cameraTransform.eulerAngles.y;
				pitch = cameraTransform.eulerAngles.x;
			}
			skipNextScroll = false;
		}

		public virtual void Initialize()
		{
			if (camera != null)
			{
				var cameraTransform = camera.transform;
				yaw = cameraTransform.eulerAngles.y;
				pitch = cameraTransform.eulerAngles.x;
			}
		}

		public virtual void Update()
		{
			if (!camera) return;

			var cameraTransform = camera.transform;

			// Handle rotation (right mouse or touch)
			bool isGuiControlActive = GUIUtility.hotControl != 0;
			if ((Input.GetMouseButton(1) || Input.touchCount > 0) && !isGuiControlActive)
			{
				float pointerX = Input.GetAxis("Mouse X");
				float pointerY = Input.GetAxis("Mouse Y");
				if (Input.touchCount > 0)
				{
					pointerX = Input.touches[0].deltaPosition.x * 0.05f;
					pointerY = Input.touches[0].deltaPosition.y * 0.05f;
				}
				yaw += lookSpeedH * pointerX;
				pitch -= lookSpeedV * pointerY;
				cameraTransform.eulerAngles = new Vector3(pitch, yaw, 0f);
			}

			// Zoom with mouse wheel
			if (InsideWindow() && !GUIManager.IsMouseOverGui() && !EventSystem.current.IsPointerOverGameObject())
			{
				float scroll = skipNextScroll ? 0f : Input.GetAxis("Mouse ScrollWheel");
				cameraTransform.Translate(0, 0, scroll * zoomSpeed, Space.Self);
				skipNextScroll = false;
				if (scroll != 0f)
				{
					Debug.Log($"Zooming: scroll={scroll}, zoomSpeed={zoomSpeed}");
				}
			}

			// Translation (WASD movement)
			Vector3 translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;
			cameraTransform.Translate(translation, Space.Self);
		}

		public virtual void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus) skipNextScroll = true;
		}

		protected bool InsideWindow()
		{
			Vector3 mousePosition = Input.mousePosition;
			return mousePosition.x >= 0 && mousePosition.x <= Screen.width &&
				   mousePosition.y >= 0 && mousePosition.y <= Screen.height;
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

			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) direction *= 5f;
			return direction;
		}
	}
}