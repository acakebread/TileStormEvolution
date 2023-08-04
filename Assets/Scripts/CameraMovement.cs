// Copyright 2019 massivehadron.com ltd. created 11/07/2019 by Andrew Cakebread

using UnityEngine;

namespace Decology
{
	public class CameraMovement : MonoBehaviour
	{
		public float lookSpeedH = 2f;
		public float lookSpeedV = 2f;
		public float zoomSpeed = 2f;
		public float dragSpeed = 6f;

		private float yaw = 0f;
		private float pitch = 0f;

		private void Start()
		{
			yaw = transform.eulerAngles.y;
			pitch = transform.eulerAngles.x;
		}

		void Update()
		{
			float pointer_x = Input.GetAxis("Mouse X");
			float pointer_y = Input.GetAxis("Mouse Y");
			if (Input.touchCount > 0)
			{
				pointer_x = Input.touches[0].deltaPosition.x * 0.05f;
				pointer_y = Input.touches[0].deltaPosition.y * 0.05f;
			}

			if (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1))
			{
				yaw += lookSpeedH * pointer_x;
				pitch -= lookSpeedV * pointer_y;

				transform.eulerAngles = new Vector3(pitch, yaw, 0f);
			}

			//drag camera around with Middle Mouse
			if (Input.GetMouseButton(2))
			{
				transform.Translate(-Input.GetAxisRaw("Mouse X") * Time.deltaTime * dragSpeed, -Input.GetAxisRaw("Mouse Y") * Time.deltaTime * dragSpeed, 0);
			}

			//Zoom in and out with Mouse Wheel
			transform.Translate(0, 0, Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, Space.Self);
		}
	}
}