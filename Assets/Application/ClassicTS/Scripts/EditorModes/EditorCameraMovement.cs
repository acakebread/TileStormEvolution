// File: EditorCameraMovement.cs
using MassiveHadronLtd;
using UnityEngine;

public static class EditorCameraMovement
{
	private const float LookSpeedH = 2f;
	private const float LookSpeedV = 2f;
	private const float ZoomSpeed = 12f;
	private const float MoveSpeedMultiplier = 1f;

	public static void UpdateCamera(Transform camTransform, bool allowInput = true)
	{
		if (camTransform == null || !allowInput) return;

		var camera = camTransform.GetComponent<Camera>();
		if (camera == null) return;

		bool isGuiActive = UnityEngine.EventSystems.EventSystem.current?.IsPointerOverGameObject() ?? false;
		bool isMouseOverGui = false; // You can extend this if needed

		// Right mouse button or touch = orbit
		if ((Input.GetMouseButton(1) || Input.touchCount > 0) && !isGuiActive)
		{
			float pointerX = Input.GetAxis("Mouse X");
			float pointerY = Input.GetAxis("Mouse Y");

			if (Input.touchCount > 0)
			{
				pointerX = Input.touches[0].deltaPosition.x * 0.05f;
				pointerY = Input.touches[0].deltaPosition.y * 0.05f;
			}

			var eulers = camTransform.eulerAngles;
			eulers.y += LookSpeedH * pointerX;
			eulers.x -= LookSpeedV * pointerY;
			camTransform.eulerAngles = eulers;
		}

		// Mouse wheel zoom (only if not over GUI)
		if (!isMouseOverGui && !isGuiActive && GuiUtils.IsMouseInsideWindow())
		{
			float scroll = Input.GetAxis("Mouse ScrollWheel");
			if (scroll != 0f)
				camTransform.Translate(0, 0, scroll * ZoomSpeed, Space.Self);
		}

		// WASDQE movement
		Vector3 translation = GetInputTranslationDirection() * ZoomSpeed * MoveSpeedMultiplier * Time.deltaTime;
		camTransform.Translate(translation, Space.Self);
	}

	private static Vector3 GetInputTranslationDirection()
	{
		Vector3 dir = Vector3.zero;
		if (Input.GetKey(KeyCode.W)) dir += Vector3.forward;
		if (Input.GetKey(KeyCode.S)) dir += Vector3.back;
		if (Input.GetKey(KeyCode.A)) dir += Vector3.left;
		if (Input.GetKey(KeyCode.D)) dir += Vector3.right;
		if (Input.GetKey(KeyCode.Q)) dir += Vector3.down;
		if (Input.GetKey(KeyCode.E)) dir += Vector3.up;

		if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
			dir *= 2f;

		return dir;
	}

	// Optional: skip next scroll on focus gain (same behavior as before)
	private static bool skipNextScroll = false;
	public static void OnApplicationFocus(bool hasFocus)
	{
		if (hasFocus) skipNextScroll = true;
	}

	public static bool ShouldSkipScroll() { bool s = skipNextScroll; skipNextScroll = false; return s; }
}