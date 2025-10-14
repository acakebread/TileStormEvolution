using UnityEngine;
using UnityEngine.EventSystems;

namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		private float yaw;
		private float pitch;
		private bool dragging;
		private bool skipNextScroll;
		private Transform cameraTransform;
		private bool isDraggingWithBothMouseButtons; // For LMB + RMB plane dragging
		private Vector2 dragStartMousePosition; // Screen-space mouse position at drag start
		private Plane dragPlane;
		private GameObject selectedObject; // Currently selected object
		private GameObject selectionMarker; // 3D marker for selected object

		// Configuration values
		private float lookSpeedH = 2f;
		private float lookSpeedV = 2f;
		private float zoomSpeed = 12f;

		public CameraEditor(CameraState state) : base(state)
		{
			cameraTransform = state.data.camera.transform;
		}

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 45f;
			data.postProcessingEnabled = false;
			data.camera.fieldOfView = data.fieldOfView;

			// Initialize rotation and state
			yaw = cameraTransform.eulerAngles.y;
			pitch = cameraTransform.eulerAngles.x;
			dragging = false;
			skipNextScroll = false;
			isDraggingWithBothMouseButtons = false;
			selectedObject = null;
			selectionMarker = null;

			// Ensure EventSystem exists
			if (!Object.FindAnyObjectByType<EventSystem>())
			{
				new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			}
		}

		public override void Update()
		{
			base.Update();

			bool wasDragging = dragging;

			// Handle mouse button down to start dragging or selection
			if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) &&
				!EventSystem.current.IsPointerOverGameObject())
			{
				dragging = true;
				yaw = cameraTransform.eulerAngles.y;
				pitch = cameraTransform.eulerAngles.x;

				// Handle LMB down for selection
				if (Input.GetMouseButtonDown(0))
				{
					Ray ray = data.camera.ScreenPointToRay(Input.mousePosition);
					if (Physics.Raycast(ray, out RaycastHit hit))
					{
						selectedObject = hit.transform.gameObject;
						CreateSelectionMarker(hit.point);
						Debug.Log($"Selected Object: {selectedObject.name}, Position: {hit.point}");
					}
					else
					{
						selectedObject = null;
						DestroySelectionMarker();
						Debug.Log("No object selected");
					}
				}

				// Handle LMB + RMB for plane dragging
				if (Input.GetMouseButton(0) && Input.GetMouseButton(1))
				{
					isDraggingWithBothMouseButtons = true;
					dragStartMousePosition = Input.mousePosition;
					Ray ray = data.camera.ScreenPointToRay(Input.mousePosition);
					if (Physics.Raycast(ray, out RaycastHit hit))
						dragPlane = new Plane(Vector3.up, new Vector3(0f, hit.point.y, 0f));
					else
						dragPlane = new Plane(Vector3.up, new Vector3(0f, 0f, 0f));
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

			// Handle camera rotation (for right mouse alone or touch dragging)
			if (dragging && wasDragging && !isDraggingWithBothMouseButtons &&
				(Input.touchCount > 0 || (Input.GetMouseButton(1) && !Input.GetMouseButton(0))))
			{
				yaw += lookSpeedH * pointerX;
				pitch -= lookSpeedV * pointerY;
				cameraTransform.eulerAngles = new Vector3(pitch, yaw, 0f);
			}
			// Handle plane-based dragging (for LMB + RMB)
			else if (dragging && wasDragging && isDraggingWithBothMouseButtons &&
					 Input.GetMouseButton(0) && Input.GetMouseButton(1))
			{
				// Calculate screen-space delta
				Vector2 delta = (Vector2)Input.mousePosition - dragStartMousePosition;

				// Calculate distance from camera to plane
				Ray ray = data.camera.ScreenPointToRay(Input.mousePosition);
				float dist = dragPlane.Raycast(ray, out float enter) ? enter : Mathf.Abs(cameraTransform.position.y - dragPlane.GetDistanceToPoint(Vector3.zero));

				// Normalize delta and scale by distance to plane
				delta /= Screen.height; // Normalize for resolution independence
				delta *= dist; // Scale for 1:1 movement
				delta = -delta; // Invert for correct drag direction

				// Convert to world-space delta
				Vector3 worldDelta = cameraTransform.right * delta.x + Vector3.Cross(cameraTransform.right, Vector3.up).normalized * delta.y;

				// Apply delta to camera position
				cameraTransform.position += worldDelta;

				// Update drag start position for next frame
				dragStartMousePosition = Input.mousePosition;
			}

			// Handle LMB release to clear selection and marker
			if (!Input.GetMouseButton(0))
			{
				if (selectedObject != null)
				{
					selectedObject = null;
					DestroySelectionMarker();
					Debug.Log("Selection cleared");
				}
			}

			// Stop dragging when both mouse buttons are released
			if (!(Input.touchCount > 0 || (Input.GetMouseButton(0) && Input.GetMouseButton(1))))
			{
				isDraggingWithBothMouseButtons = false;
			}
			if (!(Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
			{
				dragging = false;
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

		private void CreateSelectionMarker(Vector3 position)
		{
			DestroySelectionMarker(); // Clear any existing marker
			selectionMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			selectionMarker.transform.position = position;
			selectionMarker.transform.localScale = Vector3.one * 0.5f; // Small sphere
			Renderer renderer = selectionMarker.GetComponent<Renderer>();
			if (renderer != null)
			{
				renderer.material.color = Color.red; // Distinct color for visibility
			}
			selectionMarker.name = "SelectionMarker";
			Debug.Log($"Created marker at: {position}");
		}

		private void DestroySelectionMarker()
		{
			if (selectionMarker != null)
			{
				Object.Destroy(selectionMarker);
				selectionMarker = null;
			}
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

		protected override void ApplyProjection() { }
	}
}