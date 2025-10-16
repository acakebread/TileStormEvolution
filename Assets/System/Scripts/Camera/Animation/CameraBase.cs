using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected const float TargetFPS = 60f;

		protected CameraState state;

		// Moved from CameraState
		public CameraMode mode;
		public CameraData data;
		public Func<Vector3> originFn;
		public Func<Vector3> targetFn;
		public Func<IReadOnlyList<Vector3>> pointsFn;

		// Updated helper properties to use new fields when available, falling back to state
		protected Vector3 origin => originFn?.Invoke() ?? state?.origin?.Invoke() ?? Vector3.zero;
		protected Vector3 target => targetFn?.Invoke() ?? state?.target?.Invoke() ?? Vector3.zero;
		protected IReadOnlyList<Vector3> points => pointsFn?.Invoke() ?? state?.points?.Invoke() ?? Array.Empty<Vector3>();

		public CameraBase(CameraState _state) => state = _state ?? throw new ArgumentNullException(nameof(_state));

		public virtual void Start() { }
		public virtual void Update() { }
		public virtual void OnApplicationFocus(bool hasFocus) { }
		public virtual bool HasCompleted => false;

		protected virtual void ApplyProjection()
		{
			if (data?.camera == null && state?.data?.camera == null) return;
			var camera = data?.camera ?? state.data.camera;
			var dataOrigin = data?.origin ?? state.data.origin;
			var dataTarget = data?.target ?? state.data.target;
			var fieldOfView = data?.fieldOfView ?? state.data.fieldOfView;
			//var shake = data?.shake ?? state.data.shake;

			camera.transform.position = dataOrigin;
			var direction = dataTarget - dataOrigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
			//CameraUtils.ApplyCameraShake(camera, shake);
		}
	}
}