//#define MOBILE
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorCameraMovement
	{
		private const float LookSpeedH = 0.15f;
		private const float LookSpeedV = 0.15f;
		private const float MoveSpeed = 6f;

		private static float MoveSpeedModifier = 1f;
		private static float ModifiedZoomSpeed => MoveSpeed * MoveSpeedModifier;
		private static float ScrollSpeed = 0.25f;

		private static bool isPanning = false;
		private static Vector3 worldStart;
		private static bool focus = false;
		private static bool didGainFocus = true;

		public static void StartPanning(Vector3 value)
		{
			if (isPanning) return;
			isPanning = value != Vector3.negativeInfinity;
			worldStart = value;
		}

		public static void UpdateCamera(Camera camera, Vector3 worldCurrent, bool inFocus = true)
		{
			if (camera == null) return;

			var camTransform = camera.transform;

			if (InputX.GetMouseButtonDown(1))
				focus = inFocus;

			if (InputX.GetMouseButtonUp(0))
				isPanning = false;

			if (!InputX.GetMouseButton(0) && !InputX.GetMouseButton(1))
				focus = inFocus;

			if (!focus) return;

			if (didGainFocus)
			{
				didGainFocus = false;
				return;
			}

			// Panning
			if (isPanning && worldCurrent != Vector3.negativeInfinity)
			{
				camTransform.position += worldStart - worldCurrent;
			}

			float pointerX = 0f;
			float pointerY = 0f;

#if MOBILE
            if (InputX.touchCount == 2)
            {
                var t0 = InputX.touches[0];
                var t1 = InputX.touches[1];

                if ((t0.phase == TouchPhase.Stationary || t0.phase == TouchPhase.Moved) &&
                    (t1.phase == TouchPhase.Stationary || t1.phase == TouchPhase.Moved))
                {
                    pointerX = (t0.deltaPosition.x + t1.deltaPosition.x) * 0.5f;
                    pointerY = (t0.deltaPosition.y + t1.deltaPosition.y) * 0.5f;
                }
            }
#else
			// Desktop - Right mouse button look
			if (InputX.GetMouseButton(1))
			{
				//var delta = InputX.GetMouseDelta();
				//pointerX = delta.x;
				//pointerY = delta.y;
				pointerX = InputX.GetAxis("Mouse X");
				pointerY = InputX.GetAxis("Mouse Y");
			}
#endif
			//Debug.Log(pointerX);

			// Apply your compensation scalar
			pointerX *= InputX.TOUCH_LOOK_COMPENSATION_SCALAR;
			pointerY *= InputX.TOUCH_LOOK_COMPENSATION_SCALAR;

			// Your scaledLook (kept exactly as you wanted)
			float scaledLook = 1f; // 64f / Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
			pointerX *= scaledLook;
			pointerY *= scaledLook;

			// Rotation
			var eulers = camTransform.eulerAngles;
			eulers.y += LookSpeedH * pointerX;//eulers.y += LookSpeedH * pointerX * Time.deltaTime * 60f;
			eulers.x -= LookSpeedV * pointerY;//eulers.x -= LookSpeedV * pointerY * Time.deltaTime * 60f;
			eulers.x = Mathf.Clamp(Mathf.DeltaAngle(0f, eulers.x), -90f, 90f);
			camTransform.eulerAngles = eulers;

			// Tab toggle slow movement
			if (InputX.GetKeyDown(KeyCode.Tab))
				MoveSpeedModifier = 0.1f / MoveSpeedModifier;

			// WASDQE movement
			Vector3 translation = GetInputTranslationDirection() * ModifiedZoomSpeed * Time.deltaTime;

			// Mouse wheel zoom
			if (GuiUtils.IsMouseInsideWindow())
			{
				float scroll = InputX.GetAxis("Mouse ScrollWheel") * ScrollSpeed * InputX.TOUCH_SCROLL_COMPENSATION_SCALAR;
				if (scroll != 0f)
					translation += Vector3.forward * scroll * ModifiedZoomSpeed;
			}

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

		public static void OnApplicationFocus(bool hasFocus)
		{
			didGainFocus |= hasFocus;
		}
	}
}