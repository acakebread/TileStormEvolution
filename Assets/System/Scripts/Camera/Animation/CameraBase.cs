using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected const float TargetFPS = 60f;

		//protected CameraState state;
		//public CameraBase(CameraState _state) => state = _state ?? throw new ArgumentNullException(nameof(_state));

		//protected Vector3 origin => state.origin?.Invoke() ?? Vector3.zero;
		//protected Vector3 target => state.target?.Invoke() ?? Vector3.zero;
		//protected IReadOnlyList<Vector3> points => state.points?.Invoke() ?? Array.Empty<Vector3>();//focus points
		//protected CameraData data => state.data;

		//merged in from CameraState
		protected CameraData data;
		protected Func<Vector3> originFn;
		protected Func<Vector3> targetFn;
		protected Func<IReadOnlyList<Vector3>> pointsFn;

		public CameraBase(CameraState _state)
		{
			//state = _state ?? throw new ArgumentNullException(nameof(_state));
			if (null != _state)
			{
				data = _state.data;
				originFn = _state.origin;
				targetFn = _state.target;
				pointsFn = _state.points;
			}
		}

		protected Vector3 origin => originFn?.Invoke() ?? Vector3.zero;
		protected Vector3 target => targetFn?.Invoke() ?? Vector3.zero;
		protected IReadOnlyList<Vector3> points => pointsFn?.Invoke() ?? Array.Empty<Vector3>();//focus points

		public virtual void Start() { }
		public virtual void Update() { }
		public virtual void OnApplicationFocus(bool hasFocus) { }
		public virtual bool HasCompleted => false;

		protected virtual void ApplyProjection()
		{
			if (data?.camera == null) return;
			data.camera.transform.position = data.origin;
			var direction = data.target - data.origin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
			//CameraUtils.ApplyCameraShake(data.camera, data.shake);
		}
	}
}