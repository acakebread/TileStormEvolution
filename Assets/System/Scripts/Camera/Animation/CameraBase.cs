using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		public CameraData data;

		public Func<Vector3> originFunc;
		public Func<Vector3> targetFunc;
		public Func<IReadOnlyList<Vector3>> focusPointsFunc;

		public virtual void Start() { }
		public virtual void Update() { }

		public virtual bool HasCompleted => false;

		protected virtual void ApplyProjection()
		{
			if (null == data.camera) return;
			data.camera.transform.position = data.origin;
			var direction = data.target - data.origin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
			//CameraUtils.ApplyCameraShake(data.camera, data.shake);
		}
	}
}
