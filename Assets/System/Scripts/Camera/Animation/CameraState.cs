using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraState
	{
		public CameraMode mode;
		public CameraData data;
		public Func<Vector3> origin;
		public Func<Vector3> target;
		public Func<IReadOnlyList<Vector3>> points;

		public void ApplyToCamera(Camera camera)
		{
			if (camera == null) return;
			camera.transform.position = origin?.Invoke() ?? Vector3.zero;
			var direction = (target?.Invoke() ?? Vector3.zero) - camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}
	}
}