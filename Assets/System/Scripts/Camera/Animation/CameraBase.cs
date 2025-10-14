using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected const float TargetFPS = 60f;

		protected CameraState state;

		// Constructor requiring CameraState
		public CameraBase(CameraState _state) => state = _state ?? throw new ArgumentNullException(nameof(_state));

		// Helper properties for easier access to CameraState properties
		protected Vector3 origin => state.origin?.Invoke() ?? Vector3.zero;
		protected Vector3 target => state.target?.Invoke() ?? Vector3.zero;
		protected IReadOnlyList<Vector3> focusPoints => state.focusPoints?.Invoke() ?? Array.Empty<Vector3>();
		protected CameraData data => state.data;

		public virtual void Start() { }
		public virtual void Update() { }

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