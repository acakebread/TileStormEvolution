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

		public override void Start()
		{
			base.Start();
			fieldOfView = 60f;
			camera.fieldOfView = fieldOfView;
			postProcessingEnabled = false;

			// Initialize rotation and state
			yaw = camera.transform.eulerAngles.y;
			pitch = camera.transform.eulerAngles.x;
			dragging = false;
			skipNextScroll = false;

			// Ensure EventSystem exists
			if (!Object.FindAnyObjectByType<EventSystem>())
				new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
		}

		public override void Update()
		{
			base.Update();

			var wasDragging = dragging;

			// Handle mouse button down to start dragging
			if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) &&
				!EventSystem.current.IsPointerOverGameObject())
			{
				dragging = true;
				yaw = camera.transform.eulerAngles.y;
				pitch = camera.transform.eulerAngles.x;
			}

			// Get mouse or touch input
			float pointerX = Input.GetAxis("Mouse X");
			float pointerY = Input.GetAxis("Mouse Y");
			if (Input.touchCount > 0)
			{
				pointerX = Input.touches[0].deltaPosition.x * 0.05f;
				pointerY = Input.touches[0].deltaPosition.y * 0.05f;
			}

			// Handle camera rotation (for left or right mouse)
			if (dragging && wasDragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
			{
				yaw += lookSpeedH * pointerX;
				pitch -= lookSpeedV * pointerY;
				camera.transform.eulerAngles = new Vector3(pitch, yaw, 0f);
			}

			// Stop dragging when mouse buttons or touch are released
			if (!(Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
			{
				dragging = false;
			}

			// Zoom with mouse wheel
			if (InsideWindow())
			{
				float scroll = skipNextScroll ? 0f : Input.GetAxis("Mouse ScrollWheel");
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
			Vector3 mousePosition = Input.mousePosition;
			return mousePosition.x >= 0 && mousePosition.x <= Screen.width &&
				   mousePosition.y >= 0 && mousePosition.y <= Screen.height;
		}

		private Vector3 GetInputTranslationDirection()
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

		protected override void OnRender()
		{
			if (camera == null) return;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
		}
	}
}