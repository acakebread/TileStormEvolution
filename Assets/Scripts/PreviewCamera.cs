// Copyright 2018 massivehadron.com ltd. created 04/04/2018 by Andrew Cakebread

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Decology
{
	public class PreviewCamera : MonoBehaviour
	{
		//test camera 
		private GameObject camera_pivot
		{
			get
			{
				if (null == transform.Find("pivot"))
				{
					GameObject pivot = new GameObject("pivot");
					pivot.transform.SetParent(transform.parent, false);
					pivot.transform.rotation = transform.rotation;
					pivot.transform.SetParent(transform);
				}
				return transform.Find("pivot").gameObject;
			}
		}

		private Vector3 pivotAngle = Vector3.zero;
		private Vector3 rotation = new Vector3(0, 0, 0);

		private void ApplyDelta(Transform pivot,Vector3 delta)
		{
			pivotAngle += delta;
			pivotAngle.x = Mathf.Clamp(pivotAngle.x, -50, 50);
			pivotAngle.y = Mathf.Clamp(pivotAngle.y + rotation.y, -38, 0) - rotation.y;

			Vector3 combined = rotation + pivotAngle;
			pivot.rotation = new Quaternion();
			pivot.Rotate(0, combined.x, 0);
			pivot.Rotate(-combined.y, 0, 0);
		}

		private void Awake()
		{
			Vector3 euler = camera_pivot.transform.eulerAngles;
			rotation = new Vector3(normalizeAngle(euler.y), normalizeAngle(-euler.x), 0);
		}

		private Vector3 old = Vector3.zero;
		private void Update()
		{
			if (Input.GetMouseButton(0))
			{
				Transform pivot = camera_pivot.transform;
				pivot.SetParent(transform.parent);
				transform.SetParent(pivot);
				ApplyDelta(pivot, (Input.mousePosition - old) * 0.2f);
				transform.SetParent(pivot.parent);
				pivot.SetParent(transform);
			}
			old = Input.mousePosition;
		}

		//utilities
		private static float UMODF(float val, float mod) { return (val % mod + mod) % mod; }//positive modulo

		private static float normalizeAngle(float angle)
		{
			return UMODF(angle + 180, 360) - 180;
		}
	}
}