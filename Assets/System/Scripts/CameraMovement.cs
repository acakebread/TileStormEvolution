// Copyright 2019 massivehadron.com ltd. created 11/07/2019 by Andrew Cakebread

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MassiveHadronLtd
{
	public class CameraMovement : MonoBehaviour
	{
		public float lookSpeedH = 2f;
		public float lookSpeedV = 2f;
		public float zoomSpeed = 12f;
		public float dragSpeed = 18f;

		private float yaw;
		private float pitch;
		private bool dragging;
		private bool skipNextScroll; // Only for scroll wheel

		private void Awake()
		{
			yaw = transform.eulerAngles.y;
			pitch = transform.eulerAngles.x;
			dragging = false;
			skipNextScroll = false;

			// Ensure EventSystem exists
			if (!Object.FindAnyObjectByType<EventSystem>())
			{
				new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			}
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (hasFocus)
			{
				skipNextScroll = true; // Skip scroll delta on first frame after focus
			}
		}


		private void OnEnable()
		{
			StartCoroutine(Run());
			IEnumerator Run()
			{
				while (true)
				{
					yield return null;
					bool wasDragging = dragging;

					// Handle mouse button down to start dragging
					if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) &&
						!EventSystem.current.IsPointerOverGameObject())
					{
						dragging = true;
						// Update yaw and pitch to current camera rotation when dragging starts
						yaw = transform.eulerAngles.y;
						pitch = transform.eulerAngles.x;
					}

					// Get mouse or touch input
					float pointerX = Input.GetAxis("Mouse X");
					float pointerY = Input.GetAxis("Mouse Y");
					if (Input.touchCount > 0)
					{
						pointerX = Input.touches[0].deltaPosition.x * 0.05f;
						pointerY = Input.touches[0].deltaPosition.y * 0.05f;
					}

					// Handle camera rotation (skip deltas on first frame of drag)
					if (dragging && wasDragging && (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
					{
						yaw += lookSpeedH * pointerX;
						pitch -= lookSpeedV * pointerY;
						transform.eulerAngles = new Vector3(pitch, yaw, 0f);
					}
					else if (!(Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
					{
						dragging = false;
					}

					// Zoom with mouse wheel
					if (insideWindow())
					{
						float scroll = skipNextScroll ? 0f : Input.GetAxis("Mouse ScrollWheel");
						transform.Translate(0, 0, scroll * zoomSpeed, Space.Self);
						skipNextScroll = false; // Reset after scroll handling
					}

					// Translation
					Vector3 translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;
					transform.Translate(translation, Space.Self);
				}
			}
		}

		private bool insideWindow()
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

			// Apply 5x speed multiplier when shift is held
			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
			{
				direction *= 5f;
			}

			return direction;
		}
	}
}