using UnityEngine;
using UnityEngine.EventSystems;
namespace MassiveHadronLtd
{
	public class CameraDefault : CameraBase
	{
		private float yaw;
		private float pitch;
		private bool dragging;
		private bool skipNextScroll;
		private float lookSpeedH = 2f;
		private float lookSpeedV = 2f;
		private float zoomSpeed = 12f;

		public CameraDefault(Camera camera) : base(camera) { }

		public override void Awake()
		{
			base.Awake();

			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}

		public override void OnEnable()
		{
			base.OnEnable();
			fieldOfView = 60f;
			camera.fieldOfView = fieldOfView;
			postProcessingEnabled = false;

			// Initialize rotation and state
			yaw = camera.transform.eulerAngles.y;
			pitch = camera.transform.eulerAngles.x;
			dragging = false;
			skipNextScroll = false;

			// Ensure EventSystem exists
			if (!Object.FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
		}

		public override void Update()
		{
			base.Update();

			var wasDragging = dragging;

			// Handle mouse button down to start dragging
			if ((InputX.GetMouseButtonDown(0) || InputX.GetMouseButtonDown(1)) &&
				!EventSystem.current.IsPointerOverGameObject())
			{
				dragging = true;
				yaw = camera.transform.eulerAngles.y;
				pitch = camera.transform.eulerAngles.x;
			}

			// Get mouse or touch input
			float pointerX = InputX.GetAxis("Mouse X");
			float pointerY = InputX.GetAxis("Mouse Y");
			if (InputX.touchCount > 0)
			{
				pointerX = InputX.touches[0].deltaPosition.x * 0.05f;
				pointerY = InputX.touches[0].deltaPosition.y * 0.05f;
			}

			// Handle camera rotation (for left or right mouse)
			if (dragging && wasDragging && (InputX.GetMouseButton(0) || InputX.GetMouseButton(1)))
			{
				yaw += lookSpeedH * pointerX;
				pitch -= lookSpeedV * pointerY;
				camera.transform.eulerAngles = new Vector3(pitch, yaw, 0f);
			}

			// Stop dragging when mouse buttons or touch are released
			if (!(InputX.touchCount > 0 || InputX.GetMouseButton(0) || InputX.GetMouseButton(1)))
			{
				dragging = false;
			}

			// Zoom with mouse wheel
			if (InsideWindow())
			{
				float scroll = skipNextScroll ? 0f : InputX.GetAxis("Mouse ScrollWheel");
				camera.transform.Translate(0, 0, scroll * zoomSpeed, Space.Self);
				skipNextScroll = false;
			}

			// Translation (WASD movement, always enabled)
			Vector3 translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;
			camera.transform.Translate(translation, Space.Self);
		}

		public override void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus) skipNextScroll = true;
		}

		private bool InsideWindow()
		{
			Vector3 mousePosition = InputX.mousePosition;
			return mousePosition.x >= 0 && mousePosition.x <= Screen.width &&
				   mousePosition.y >= 0 && mousePosition.y <= Screen.height;
		}

		private Vector3 GetInputTranslationDirection()
		{
			Vector3 direction = Vector3.zero;
			if (InputX.GetKey(KeyCode.W)) direction += Vector3.forward;
			if (InputX.GetKey(KeyCode.S)) direction += Vector3.back;
			if (InputX.GetKey(KeyCode.A)) direction += Vector3.left;
			if (InputX.GetKey(KeyCode.D)) direction += Vector3.right;
			if (InputX.GetKey(KeyCode.Q)) direction += Vector3.down;
			if (InputX.GetKey(KeyCode.E)) direction += Vector3.up;

			if (InputX.GetKey(KeyCode.LeftShift) || InputX.GetKey(KeyCode.RightShift)) direction *= 5f;

			return direction;
		}
	}
}