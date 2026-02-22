using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorCameraMovement
	{
		private const float LookSpeedH = 4f;
		private const float LookSpeedV = 4f;
		private const float MoveSpeed = 8f;
		private const float TouchCompensation = 4f;
		private static float MoveSpeedModifier = 1f;
		private static float ModifiedZoomSpeed => MoveSpeed * MoveSpeedModifier;

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
			//if (focus && (InputX.GetMouseButton(1) || InputX.touchCount > 1))

#if true//!UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
			if (focus && InputX.touchCount == 2)
			{
				const float LINEAR_TOUCH_COMPENSATION = 4f;

				float pointerX = 0f;
				float pointerY = 0f;
				if ((InputX.touches[0].phase == TouchPhase.Stationary || InputX.touches[0].phase == TouchPhase.Moved) &&
					(InputX.touches[1].phase == TouchPhase.Stationary || InputX.touches[1].phase == TouchPhase.Moved))
				{
					pointerX = (InputX.touches[0].deltaPosition.x + InputX.touches[1].deltaPosition.x) / LINEAR_TOUCH_COMPENSATION;
					pointerY = (InputX.touches[0].deltaPosition.y + InputX.touches[1].deltaPosition.y) / LINEAR_TOUCH_COMPENSATION;
				}
#else
			if (focus && InputX.GetMouseButton(1))
			{
				float pointerX = InputX.GetAxis("Mouse X");
				float pointerY = InputX.GetAxis("Mouse Y");

#endif
				float scaledLook = 1024f / Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
//#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
//				if (Application.isMobilePlatform) scaledLook /= TouchCompensation;
//#endif

				pointerX *= scaledLook;
				pointerY *= scaledLook;

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
			Vector3 translation = GetInputTranslationDirection() * ModifiedZoomSpeed * Time.deltaTime;

			// Mouse wheel zoom (only if not over GUI)
			if (!isMouseOverGui && GuiUtils.IsMouseInsideWindow())
			{
				var scroll = InputX.GetAxis("Mouse ScrollWheel") * TouchCompensation;
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
		public static void OnApplicationFocus(bool hasFocus) => didGainFocus |= hasFocus;
	}
}