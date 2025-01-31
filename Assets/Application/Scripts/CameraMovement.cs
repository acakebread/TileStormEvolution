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
		public float zoomSpeed = 1f;
		public float dragSpeed = 6f;

		private IEnumerator Start()
		{
			var yaw = transform.eulerAngles.y;
			var pitch = transform.eulerAngles.x;
			var dragging = false;

			while (true)
			{
				yield return null;

				if (null == EventSystem.current) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
				if ((true == Input.GetMouseButtonDown(0) || true == Input.GetMouseButtonDown(1)) && false == EventSystem.current.IsPointerOverGameObject()) dragging = true;
				//Debug.Log(EventSystem.current);

				float pointer_x = Input.GetAxis("Mouse X");
				float pointer_y = Input.GetAxis("Mouse Y");
				if (Input.touchCount > 0)
				{
					pointer_x = Input.touches[0].deltaPosition.x * 0.05f;
					pointer_y = Input.touches[0].deltaPosition.y * 0.05f;
				}

				if (true == dragging && (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
				{
					yaw += lookSpeedH * pointer_x;
					pitch -= lookSpeedV * pointer_y;

					transform.eulerAngles = new Vector3(pitch, yaw, 0f);
				}
				else
					dragging = false;


				//Zoom in and out with Mouse Wheel
				transform.Translate(0, 0, Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, Space.Self);

				// Translation
				var translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;

				transform.Translate(translation, Space.Self);
			}
		}

		//private void Start()
		//{
		//	yaw = transform.eulerAngles.y;
		//	pitch = transform.eulerAngles.x;
		//}

		//void Update()
		//{
		//	if ((true == Input.GetMouseButtonDown(0) || true == Input.GetMouseButtonDown(1)) && false == EventSystem.current.IsPointerOverGameObject()) dragging = true;

		//	float pointer_x = Input.GetAxis("Mouse X");
		//	float pointer_y = Input.GetAxis("Mouse Y");
		//	if (Input.touchCount > 0)
		//	{
		//		pointer_x = Input.touches[0].deltaPosition.x * 0.05f;
		//		pointer_y = Input.touches[0].deltaPosition.y * 0.05f;
		//	}

		//	if (true == dragging && (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
		//	{
		//		yaw += lookSpeedH * pointer_x;
		//		pitch -= lookSpeedV * pointer_y;

		//		transform.eulerAngles = new Vector3(pitch, yaw, 0f);
		//	}
		//	else
		//		dragging = false;

		//	////drag camera around with Middle Mouse
		//	//if (Input.GetMouseButton(2))
		//	//{
		//	//	transform.Translate(-Input.GetAxisRaw("Mouse X") * Time.deltaTime * dragSpeed, -Input.GetAxisRaw("Mouse Y") * Time.deltaTime * dragSpeed, 0);
		//	//}

		//	//Zoom in and out with Mouse Wheel
		//	transform.Translate(0, 0, Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, Space.Self);

		//	// Translation
		//	var translation = GetInputTranslationDirection() * zoomSpeed * Time.deltaTime;

		//	transform.Translate(translation, Space.Self);
		//}

		Vector3 GetInputTranslationDirection()
		{
			Vector3 direction = new Vector3();
			if (Input.GetKey(KeyCode.W))
			{
				direction += Vector3.forward;
			}
			if (Input.GetKey(KeyCode.S))
			{
				direction += Vector3.back;
			}
			if (Input.GetKey(KeyCode.A))
			{
				direction += Vector3.left;
			}
			if (Input.GetKey(KeyCode.D))
			{
				direction += Vector3.right;
			}
			if (Input.GetKey(KeyCode.Q))
			{
				direction += Vector3.down;
			}
			if (Input.GetKey(KeyCode.E))
			{
				direction += Vector3.up;
			}
			return direction;
		}
	}
}