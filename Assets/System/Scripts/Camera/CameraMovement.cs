// Copyright 2019 massivehadron.com ltd. created 11/07/2019 by Andrew Cakebread
// Updated for Unity New Input System - Direct Device Access + original scaling restored

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MassiveHadronLtd
{
	public class CameraMovement : MonoBehaviour
	{
		public float lookSpeedH = 2f;
		public float lookSpeedV = 2f;
		public float zoomSpeed = 12f;
		public float dragSpeed = 18f;   // kept for future compatibility

		private float yaw;
		private float pitch;
		private bool dragging;
		private bool skipNextScroll;

		private void Awake()
		{
			yaw = transform.eulerAngles.y;
			pitch = transform.eulerAngles.x;
			dragging = false;
			skipNextScroll = false;
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus)
				skipNextScroll = true;
		}

		private void Update()
		{
			bool wasDragging = dragging;

			// Start dragging (Left or Right mouse button, not over UI)
			if ((Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame) &&
				!IsPointerOverUI())
			{
				dragging = true;
				yaw = transform.eulerAngles.y;
				pitch = transform.eulerAngles.x;
			}

			// Stop dragging
			if (!Mouse.current.leftButton.isPressed && !Mouse.current.rightButton.isPressed)
			{
				dragging = false;
			}

			// === Rotation while dragging ===
			if (dragging && wasDragging)
			{
				Vector2 delta = Mouse.current.delta.ReadValue();

				// Touch support with your original scaling
				if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
				{
					delta = Touchscreen.current.primaryTouch.delta.ReadValue() * 0.05f;
				}
				else
				{
					// Mouse scaling to match old Input.GetAxis("Mouse X/Y") behavior
					// Old system applied ~0.05f internally on Windows for Mouse X/Y
					delta *= 0.05f;
				}

				yaw += lookSpeedH * delta.x;
				pitch -= lookSpeedV * delta.y;

				transform.eulerAngles = new Vector3(pitch, yaw, 0f);
			}

			// === Zoom with mouse scroll ===
			if (InsideWindow())
			{
				float scroll = skipNextScroll ? 0f : Mouse.current.scroll.ReadValue().y;
				transform.Translate(0, 0, scroll * zoomSpeed, Space.Self);
				skipNextScroll = false;
			}

			// === Keyboard translation (WASD + Q/E) ===
			Vector3 translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;
			transform.Translate(translation, Space.Self);
		}

		private bool IsPointerOverUI()
		{
			if (EventSystem.current == null) return false;
			return EventSystem.current.IsPointerOverGameObject();
		}

		private bool InsideWindow()
		{
			Vector2 mousePos = Mouse.current.position.ReadValue();
			return mousePos.x >= 0 && mousePos.x <= Screen.width &&
				   mousePos.y >= 0 && mousePos.y <= Screen.height;
		}

		private Vector3 GetInputTranslationDirection()
		{
			Vector3 direction = Vector3.zero;
			var kb = Keyboard.current;

			if (kb.wKey.isPressed) direction += Vector3.forward;
			if (kb.sKey.isPressed) direction += Vector3.back;
			if (kb.aKey.isPressed) direction += Vector3.left;
			if (kb.dKey.isPressed) direction += Vector3.right;
			if (kb.qKey.isPressed) direction += Vector3.down;
			if (kb.eKey.isPressed) direction += Vector3.up;

			if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
				direction *= 5f;

			return direction;
		}
	}
}