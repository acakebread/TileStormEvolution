using UnityEngine;

namespace MassiveHadronLtd
{
	public class TestCommandCamera
	{
		public Matrix4x4 viewMatrix = Matrix4x4.identity;
		public Matrix4x4 projectionMatrix = Matrix4x4.identity;
		public RenderTexture targetTexture;

		// Core camera state
		public Vector3 position = Vector3.zero;
		public Quaternion rotation = Quaternion.identity;

		public float aspect = 16f / 9f;
		public float nearClipPlane = 0.1f;
		public float farClipPlane = 100f;

		public bool orthographic = false;
		public float orthographicSize = 5f;
		public float fieldOfView = 60f;

		public void RecalculateMatrices()
		{
			if (orthographic)
			{
				projectionMatrix = Matrix4x4.Ortho(
					-aspect * orthographicSize,
					 aspect * orthographicSize,
					-orthographicSize,
					 orthographicSize,
					nearClipPlane, farClipPlane);
			}
			else
			{
				projectionMatrix = Matrix4x4.Perspective(
					fieldOfView, aspect, nearClipPlane, farClipPlane);
			}

			viewMatrix = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
		}

		//// ────────────────────────────────────────────────────────────────
		//// Camera movement controls (copied from your system)
		//// ────────────────────────────────────────────────────────────────
		//[Header("Movement Settings")]
		//public float lookSpeedH = 2.0f;   // horizontal look speed
		//public float lookSpeedV = 2.0f;   // vertical look speed
		//public float zoomSpeed = 10.0f;   // zoom / move speed

		//// Internal state for drag handling
		//private bool dragging = false;
		//private float yaw = 0f;
		//private float pitch = 0f;

		//// ────────────────────────────────────────────────────────────────
		//// Main update – call this every frame from the test script
		//// ────────────────────────────────────────────────────────────────
		//public void Update()
		//{
		//	bool wasDragging = dragging;

		//	// Handle mouse button down to start dragging
		//	if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
		//	{
		//		dragging = true;
		//		// Initialize yaw/pitch from current rotation when drag starts
		//		Vector3 euler = rotation.eulerAngles;
		//		yaw = euler.y;
		//		pitch = euler.x;
		//	}

		//	// Get mouse or touch delta
		//	float pointerX = Input.GetAxis("Mouse X");
		//	float pointerY = Input.GetAxis("Mouse Y");
		//	if (Input.touchCount > 0)
		//	{
		//		pointerX = Input.touches[0].deltaPosition.x * 0.05f;
		//		pointerY = Input.touches[0].deltaPosition.y * 0.05f;
		//	}

		//	// Apply rotation only after first drag frame (skip deltas on press frame)
		//	if (dragging && wasDragging && (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
		//	{
		//		yaw += lookSpeedH * pointerX;
		//		pitch -= lookSpeedV * pointerY;
		//		rotation = Quaternion.Euler(pitch, yaw, 0f);
		//	}
		//	else if (!(Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
		//	{
		//		dragging = false;
		//	}

		//	// Zoom with mouse wheel
		//	float scroll = Input.GetAxis("Mouse ScrollWheel");
		//	if (scroll != 0)
		//	{
		//		position += rotation * Vector3.forward * scroll * zoomSpeed;
		//	}

		//	// Translation (WASD / arrow keys)
		//	Vector3 translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;
		//	position += rotation * translation;

		//	RecalculateMatrices();
		//}

		//// Helper – same as your original
		//private Vector3 GetInputTranslationDirection()
		//{
		//	Vector3 direction = Vector3.zero;
		//	if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction += Vector3.forward;
		//	if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction += Vector3.back;
		//	if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction += Vector3.left;
		//	if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction += Vector3.right;
		//	return direction.normalized;
		//}
	}
}