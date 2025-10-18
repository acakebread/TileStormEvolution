using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		protected Func<Vector3> originFn;
		protected Func<Vector3> targetFn;
		protected Func<IReadOnlyList<Vector3>> pointsFn;
		protected Vector3 origin => originFn?.Invoke() ?? Vector3.zero;
		protected Vector3 target => targetFn?.Invoke() ?? Vector3.zero;
		protected IReadOnlyList<Vector3> points => pointsFn?.Invoke() ?? Array.Empty<Vector3>();//focus points

		private float yaw;
		private float pitch;
		private bool dragging;
		private bool skipNextScroll;
		private Transform cameraTransform;
		private bool isDraggingWithLeftMouse;
		private Vector3 dragStartWorldPoint; // World-space point on the plane where drag starts
		private Vector3 cameraStartPosition;
		private Plane dragPlane;    // Configuration values
		private float lookSpeedH = 2f;
		private float lookSpeedV = 2f;
		private float zoomSpeed = 12f;

		public CameraEditor(CameraConfig config) : base(config)
		{
			if (null != config)
			{
				data = config.data;
				originFn = config.origin;
				targetFn = config.target;
				pointsFn = config.points;
			}

			cameraTransform = config.data.camera.transform;
		}

		public override void Awake()
		{
			//initialise camera
			var camera = data.camera;
			if (camera == null) return;
			camera.transform.position = originFn?.Invoke() ?? data.origin;
			var direction = (targetFn?.Invoke() ?? data.target) - camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 60f;
			data.postProcessingEnabled = false;
			data.camera.fieldOfView = data.fieldOfView;

			// Initialize rotation and state
			yaw = cameraTransform.eulerAngles.y;
			pitch = cameraTransform.eulerAngles.x;
			dragging = false;
			skipNextScroll = false;
			isDraggingWithLeftMouse = false;

			// Ensure EventSystem exists
			if (!UnityEngine.Object.FindAnyObjectByType<EventSystem>())
			{
				new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			}
		}

		public override void Update()
		{
			base.Update();

			bool wasDragging = dragging;

			// Handle mouse button down to start dragging (left or right mouse)
			if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) &&
				!EventSystem.current.IsPointerOverGameObject())
			{
				dragging = true;
				yaw = cameraTransform.eulerAngles.y;
				pitch = cameraTransform.eulerAngles.x;

				// If left mouse button, start dragging
				if (Input.GetMouseButtonDown(0))
				{
					isDraggingWithLeftMouse = true;
					cameraStartPosition = cameraTransform.position;
					Ray ray = data.camera.ScreenPointToRay(Input.mousePosition);
					if (Physics.Raycast(ray, out RaycastHit hit))
					{
						// Set the drag plane height to the y-position of the hit point
						dragPlane = new Plane(Vector3.up, new Vector3(0f, hit.point.y, 0f));
						dragStartWorldPoint = hit.point;
					}
					else
					{
						// Fallback to default plane (y = 0f)
						dragPlane = new Plane(Vector3.up, new Vector3(0f, 0f, 0f));
						if (dragPlane.Raycast(ray, out float enter))
							dragStartWorldPoint = ray.GetPoint(enter);
						else
							dragStartWorldPoint = cameraTransform.position;
					}
				}
			}

			// Get mouse or touch input
			float pointerX = Input.GetAxis("Mouse X");
			float pointerY = Input.GetAxis("Mouse Y");
			if (Input.touchCount > 0)
			{
				pointerX = Input.touches[0].deltaPosition.x * 0.05f;
				pointerY = Input.touches[0].deltaPosition.y * 0.05f;
			}

			// Handle camera rotation (for right mouse or touch dragging)
			if (dragging && wasDragging && !isDraggingWithLeftMouse &&
				(Input.touchCount > 0 || Input.GetMouseButton(1)))
			{
				yaw += lookSpeedH * pointerX;
				pitch -= lookSpeedV * pointerY;
				cameraTransform.eulerAngles = new Vector3(pitch, yaw, 0f);
			}
			// Handle plane-based dragging (for left mouse)
			else if (dragging && wasDragging && isDraggingWithLeftMouse && Input.GetMouseButton(0))
			{
				// Current ray from mouse position

				cameraTransform.position = cameraStartPosition;
				Ray currentRay = data.camera.ScreenPointToRay(Input.mousePosition);
				if (dragPlane.Raycast(currentRay, out float enter))
				{
					Vector3 currentWorldPoint = currentRay.GetPoint(enter);
					// Move camera to keep dragStartWorldPoint under the mouse
					Vector3 delta = dragStartWorldPoint - currentWorldPoint;
					cameraTransform.position += delta;
				}
			}

			// Stop dragging when mouse buttons or touch are released
			if (!(Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
			{
				dragging = false;
				isDraggingWithLeftMouse = false;
			}

			// Zoom with mouse wheel
			if (InsideWindow())
			{
				float scroll = skipNextScroll ? 0f : Input.GetAxis("Mouse ScrollWheel");
				cameraTransform.Translate(0, 0, scroll * zoomSpeed, Space.Self);
				skipNextScroll = false;
			}

			// Translation (WASD movement, always enabled)
			Vector3 translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;
			cameraTransform.Translate(translation, Space.Self);
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

		protected override void OnRender() { }
	}
}

