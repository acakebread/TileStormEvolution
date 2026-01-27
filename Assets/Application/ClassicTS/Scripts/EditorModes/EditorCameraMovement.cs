using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorCameraMovement
	{
		private const float LookSpeedH = 2f;
		private const float LookSpeedV = 2f;
		private const float ZoomSpeed = 12f;
		private const float MoveSpeedMultiplier = 1f;
		private static float MoveSpeedModifier = 1f;

		private static bool focus = false;
		public static void UpdateCamera(Transform camTransform, bool isMouseOverGui = false, bool allowInput = true)
		{
			if (camTransform == null || !allowInput) return;

			var camera = camTransform.GetComponent<Camera>();
			if (camera == null) return;

			if ((Input.GetMouseButtonDown(1) || Input.touchCount > 0))
				focus = !isMouseOverGui;

			if (didGainFocus)
			{
				didGainFocus = false;
				return;
			}

			// Right mouse button or touch = orbit
			if (focus && (Input.GetMouseButton(1) || Input.touchCount > 0))
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
				eulers.x = Mathf.Clamp(Mathf.DeltaAngle(0f, eulers.x), -90f, 90f);
				camTransform.eulerAngles = eulers;
			}

			if (Input.GetMouseButtonUp(1))// || Input.touchCount == 0)//this doesn't work for obvious reasons if (Input.GetMouseButtonUp(1) || Input.touchCount == 0)
				focus = false;

			if (Input.GetKeyDown(KeyCode.Tab))
				MoveSpeedModifier = 0.1f / MoveSpeedModifier;

			// WASDQE movement
			Vector3 translation = GetInputTranslationDirection() * ZoomSpeed * MoveSpeedMultiplier * MoveSpeedModifier * Time.deltaTime;

			// Mouse wheel zoom (only if not over GUI)
			if (!isMouseOverGui && GuiUtils.IsMouseInsideWindow())
			{
				float scroll = Input.GetAxis("Mouse ScrollWheel");
				if (scroll != 0f)
					translation += Vector3.forward * scroll * ZoomSpeed * MoveSpeedMultiplier * MoveSpeedModifier; //camTransform.Translate(0, 0, scroll * ZoomSpeed * MoveSpeedMultiplier * MoveSpeedModifier, Space.Self);
			}

			if (!isMouseOverGui)
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
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) dir *= 2f;
			return dir;
		}

		private static bool didGainFocus = true;
		public static void OnApplicationFocus(bool hasFocus) => didGainFocus |= hasFocus;
	}
}