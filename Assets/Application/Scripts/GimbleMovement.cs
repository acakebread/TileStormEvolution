// Copyright 2022 massivehadron.com ltd. created 30/09/2022 by Andrew Cakebread

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MassiveHadronLtd
{
	public class GimbleMovement : MonoBehaviour
	{
		public float lookSpeedH = 2f;
		public float lookSpeedV = 2f;
		public float zoomSpeed = 1f;
		public float dragSpeed = 6f;

		private float yaw = 0;// transform.eulerAngles.y;
		private float pitch = 0;//transform.eulerAngles.x;
		private bool dragging = false;

		private string msg;

		private void Start()
		{
			yaw = transform.eulerAngles.y;
			pitch = transform.eulerAngles.x;
		}

		public void Look(Vector2 delta)
		{
			yaw += lookSpeedH * delta.x;
			pitch -= lookSpeedV * delta.y;

			yaw = Mathf.Clamp(yaw, -85f, 85f);
			pitch = Mathf.Clamp(pitch, -70f, 70f);

			transform.rotation = Quaternion.identity;
			transform.Rotate(Vector3.up, yaw, Space.Self);
			transform.Rotate(Vector3.right, pitch, Space.Self);
		}

		private void _Update()
		{
			bool old_dragging = dragging;
			if ((true == Input.GetMouseButtonDown(0) || true == Input.GetMouseButtonDown(1)) && false == EventSystem.current.IsPointerOverGameObject()) dragging = true;

			float pointer_x = Input.GetAxis("Mouse X");
			float pointer_y = Input.GetAxis("Mouse Y");

			//float pointer_x = Input.GetAxis("Horizontal") * 100;
			//float pointer_y = Input.GetAxis("Vertical") * 100;

			if (Input.touchCount > 0)
			{
				pointer_x = Input.touches[0].deltaPosition.x * 0.1f * -1;
				pointer_y = Input.touches[0].deltaPosition.y * 0.1f * -1;
			}

			//Debug.Log(Input.GetAxis("Mouse X"));
			//msg = "x: " + pointer_x.ToString();

			if (true == dragging && (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
			{
				if (old_dragging == dragging)
				{
					yaw += lookSpeedH * pointer_x;
					pitch -= lookSpeedV * pointer_y;

					yaw = Mathf.Clamp(yaw, -85f, 85f);
					pitch = Mathf.Clamp(pitch, -70f, 70f);

					transform.rotation = Quaternion.identity;
					transform.Rotate(Vector3.up, yaw, Space.Self);
					transform.Rotate(Vector3.right, pitch, Space.Self);
				}
			}
			else
				dragging = false;


			//Zoom in and out with Mouse Wheel
			//transform.Translate(0, 0, Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, Space.Self);

			var _camera = transform.Find("Main Camera");
			_camera.transform.localPosition = new Vector3(0, 0, Mathf.Clamp(_camera.localPosition.z + Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, -4f, -0.5f));

			//// Translation
			//transform.Translate(GetInputTranslationDirection() * zoomSpeed * Time.deltaTime, Space.Self);

			////local function
			//Vector3 GetInputTranslationDirection()
			//{
			//	Vector3 direction = new Vector3();
			//	if (Input.GetKey(KeyCode.W)) direction += Vector3.forward;
			//	if (Input.GetKey(KeyCode.S)) direction += Vector3.back;
			//	if (Input.GetKey(KeyCode.A)) direction += Vector3.left;
			//	if (Input.GetKey(KeyCode.D)) direction += Vector3.right;
			//	if (Input.GetKey(KeyCode.Q)) direction += Vector3.down;
			//	if (Input.GetKey(KeyCode.E)) direction += Vector3.up;
			//	return direction;
			//}
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.black;
			GUI.Label(new Rect(10, 10, 100, 100), msg);
		}




		//private IEnumerator Start()
		//{
		//	var yaw = transform.eulerAngles.y;
		//	var pitch = transform.eulerAngles.x;
		//	var dragging = false;

		//	while (true)
		//	{
		//		//yield return null;
		//		yield return new WaitForEndOfFrame();

		//		bool old_dragging = dragging;
		//		if ((true == Input.GetMouseButtonDown(0) || true == Input.GetMouseButtonDown(1)) && false == EventSystem.current.IsPointerOverGameObject()) dragging = true;

		//		float pointer_x = Input.GetAxis("Mouse X");
		//		float pointer_y = Input.GetAxis("Mouse Y");

		//		//float pointer_x = Input.GetAxis("Horizontal") * 100;
		//		//float pointer_y = Input.GetAxis("Vertical") * 100;

		//		Debug.Log(Input.GetAxis("Mouse X"));

		//		if (Input.touchCount > 0)
		//		{
		//			pointer_x = Input.touches[0].deltaPosition.x * 0.05f;
		//			pointer_y = Input.touches[0].deltaPosition.y * 0.05f;
		//		}

		//		if (true == dragging && (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1)))
		//		{
		//			if (old_dragging == dragging)
		//			{
		//				yaw += lookSpeedH * pointer_x;
		//				pitch -= lookSpeedV * pointer_y;

		//				yaw = Mathf.Clamp(yaw, -80f, 80f);
		//				pitch = Mathf.Clamp(pitch, -70f, 70f);

		//				transform.rotation = Quaternion.identity;
		//				transform.Rotate(Vector3.up, yaw, Space.Self);
		//				transform.Rotate(Vector3.right, pitch, Space.Self);
		//			}
		//		}
		//		else
		//			dragging = false;


		//		//Zoom in and out with Mouse Wheel
		//		//transform.Translate(0, 0, Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, Space.Self);

		//		var _camera = transform.Find("Main Camera");
		//		_camera.transform.localPosition = new Vector3(0, 0, Mathf.Clamp(_camera.localPosition.z + Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, -4f, -0.5f));

		//		//// Translation
		//		//transform.Translate(GetInputTranslationDirection() * zoomSpeed * Time.deltaTime, Space.Self);

		//		////local function
		//		//Vector3 GetInputTranslationDirection()
		//		//{
		//		//	Vector3 direction = new Vector3();
		//		//	if (Input.GetKey(KeyCode.W)) direction += Vector3.forward;
		//		//	if (Input.GetKey(KeyCode.S)) direction += Vector3.back;
		//		//	if (Input.GetKey(KeyCode.A)) direction += Vector3.left;
		//		//	if (Input.GetKey(KeyCode.D)) direction += Vector3.right;
		//		//	if (Input.GetKey(KeyCode.Q)) direction += Vector3.down;
		//		//	if (Input.GetKey(KeyCode.E)) direction += Vector3.up;
		//		//	return direction;
		//		//}
		//	}
		//}
	}
}