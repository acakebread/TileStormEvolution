#define MOBILE

using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorCameraMovement
	{
		private const float LookSpeedH = 4f;
		private const float LookSpeedV = 4f;
		private const float MoveSpeed = 8f;
		private static float MoveSpeedModifier = 1f;
		private static float ModifiedZoomSpeed => MoveSpeed * MoveSpeedModifier;
		private static float ScrollSpeed = 1f;

		public static bool isPanning = false;
		private static Vector3 worldStart;

		private static bool focus = false;

		public static void StartPanning(Vector3 value)
		{
			if (isPanning) return;
			isPanning = value != Vector3.negativeInfinity;
			worldStart = value;
		}

		public static void UpdateCamera(Transform camTransform, Vector3 worldCurrent, bool isMouseOverGui = false, bool allowInput = true)
		{
			if (camTransform == null || !allowInput) return;

			if (isPanning)
			{
				if (worldCurrent != Vector3.negativeInfinity)
					camTransform.position += worldStart - worldCurrent;
			}

			var camera = camTransform.GetComponent<Camera>();
			if (camera == null) return;

			if (InputX.GetMouseButtonDown(1) || InputX.touchCount > 0)
				focus = !isMouseOverGui;

			if (didGainFocus)
			{
				didGainFocus = false;
				return;
			}

			if (focus)
			{
				float pointerX = 0f;
				float pointerY = 0f;

#if MOBILE//!UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
				if (InputX.touchCount == 2)
				{
					if ((InputX.touches[0].phase == TouchPhase.Stationary || InputX.touches[0].phase == TouchPhase.Moved) &&
						(InputX.touches[1].phase == TouchPhase.Stationary || InputX.touches[1].phase == TouchPhase.Moved))
					{
						pointerX = (InputX.touches[0].deltaPosition.x + InputX.touches[1].deltaPosition.x) * 0.5f;
						pointerY = (InputX.touches[0].deltaPosition.y + InputX.touches[1].deltaPosition.y) * 0.5f;
					}
				}
#else
				if (InputX.GetMouseButton(1))
				{
					pointerX = InputX.GetAxis("Mouse X");
					pointerY = InputX.GetAxis("Mouse Y");
				}
#endif

				pointerX *= InputX.TOUCH_LOOK_COMPENSATION_SCALAR;
				pointerY *= InputX.TOUCH_LOOK_COMPENSATION_SCALAR;

				float scaledLook = 64f / Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);

				pointerX *= scaledLook;
				pointerY *= scaledLook;

				var eulers = camTransform.eulerAngles;
				eulers.y += LookSpeedH * pointerX;
				eulers.x -= LookSpeedV * pointerY;
				eulers.x = Mathf.Clamp(Mathf.DeltaAngle(0f, eulers.x), -90f, 90f);
				camTransform.eulerAngles = eulers;
			}

			if (InputX.GetMouseButtonUp(0))
				isPanning = false;

			if (InputX.GetMouseButtonUp(1))// || InputX.touchCount == 0)//this doesn't work for obvious reasons if (InputX.GetMouseButtonUp(1) || InputX.touchCount == 0)
				focus = false;

			if (InputX.GetKeyDown(KeyCode.Tab))
				MoveSpeedModifier = 0.1f / MoveSpeedModifier;

			// WASDQE movement
			Vector3 translation = GetInputTranslationDirection() * ModifiedZoomSpeed * Time.deltaTime;

			// Mouse wheel zoom (only if not over GUI)
			if (!isMouseOverGui && GuiUtils.IsMouseInsideWindow())
			{
				var scroll = InputX.GetAxis("Mouse ScrollWheel") * ScrollSpeed * InputX.TOUCH_SCROLL_COMPENSATION_SCALAR;
				if (scroll != 0f)
					translation += Vector3.forward * scroll * ModifiedZoomSpeed;
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
		public static void OnApplicationFocus(bool hasFocus) => didGainFocus |= hasFocus;// this doesn't seem to work anyway so disabled in EditorController for now
	}
}