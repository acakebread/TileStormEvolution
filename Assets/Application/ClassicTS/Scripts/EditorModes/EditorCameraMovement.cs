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

			if ((InputX.GetMouseButtonDown(1) || InputX.touchCount > 0))
				focus = !isMouseOverGui;

			if (didGainFocus)
			{
				didGainFocus = false;
				return;
			}

			// Right mouse button or touch = orbit
			if (focus && (InputX.GetMouseButton(1) || InputX.touchCount > 1))
			{
				float pointerX = InputX.GetAxis("Mouse X");
				float pointerY = InputX.GetAxis("Mouse Y");

				if (InputX.touchCount > 1)
				{
					//pointerX = InputX.touches[1].deltaPosition.x * 0.05f;
					//pointerY = InputX.touches[1].deltaPosition.y * 0.05f;
					pointerX = (InputX.touches[0].deltaPosition.x + InputX.touches[1].deltaPosition.x) * 0.5f;
					pointerY = (InputX.touches[0].deltaPosition.y + InputX.touches[1].deltaPosition.y) * 0.5f;
				}

				var eulers = camTransform.eulerAngles;
				eulers.y += LookSpeedH * pointerX;
				eulers.x -= LookSpeedV * pointerY;
				eulers.x = Mathf.Clamp(Mathf.DeltaAngle(0f, eulers.x), -90f, 90f);
				camTransform.eulerAngles = eulers;
			}

			if (InputX.GetMouseButtonUp(1))// || InputX.touchCount == 0)//this doesn't work for obvious reasons if (InputX.GetMouseButtonUp(1) || InputX.touchCount == 0)
				focus = false;

			if (InputX.GetKeyDown(KeyCode.Tab))
				MoveSpeedModifier = 0.1f / MoveSpeedModifier;

			// WASDQE movement
			Vector3 translation = GetInputTranslationDirection() * ZoomSpeed * MoveSpeedMultiplier * MoveSpeedModifier * Time.deltaTime;

			// Mouse wheel zoom (only if not over GUI)
			if (!isMouseOverGui && GuiUtils.IsMouseInsideWindow())
			{
				const float scrollSensitivity = 0.02f; // ≈ 3–4× typical mouse notch

				float scroll = InputX.GetAxis("Mouse ScrollWheel");
				if (scroll != 0f)
					translation += Vector3.forward * scroll * scrollSensitivity * ZoomSpeed * MoveSpeedMultiplier * MoveSpeedModifier * Time.deltaTime; //camTransform.Translate(0, 0, scroll * ZoomSpeed * MoveSpeedMultiplier * MoveSpeedModifier, Space.Self);
			}

			if (!isMouseOverGui)
				camTransform.Translate(translation, Space.Self);
		}

		private static Vector3 GetInputTranslationDirection()
		{
			Vector3 dir = Vector3.zero;
			if (InputX.GetKey(KeyCode.W)) dir += Vector3.forward;
			if (InputX.GetKey(KeyCode.S)) dir += Vector3.back;
			if (InputX.GetKey(KeyCode.A)) dir += Vector3.left;
			if (InputX.GetKey(KeyCode.D)) dir += Vector3.right;
			if (InputX.GetKey(KeyCode.Q)) dir += Vector3.down;
			if (InputX.GetKey(KeyCode.E)) dir += Vector3.up;
			if (InputX.GetKey(KeyCode.LeftShift) || InputX.GetKey(KeyCode.RightShift)) dir *= 2f;
			return dir;
		}

		private static bool didGainFocus = true;
		public static void OnApplicationFocus(bool hasFocus) => didGainFocus |= hasFocus;
	}
}