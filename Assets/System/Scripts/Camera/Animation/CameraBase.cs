using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected CameraData _data;

		protected virtual void Start() { }
		public virtual void Start(ref CameraData data)
		{
			_data = data;
			Start();
			if (null != data.postProcessingCameraController) data.postProcessingCameraController.enabled = data.enablePostProcessing;
			HasStarted = true;
		}

		protected virtual void Update() { }
		public virtual void Update(ref CameraData data)
		{
			_data = data;
			if (!HasStarted) Start(ref _data);
			Update();
			ApplyProjection(_data);
		}

		protected virtual void ApplyProjection(CameraData data)
		{
			if (data.camera == null) return;
			data.camera.transform.position = data.lerpedPosition;
			var direction = data.lerpedTarget - data.lerpedPosition;
			if (direction.sqrMagnitude > Mathf.Epsilon) data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
			//CameraUtils.ApplyCameraShake(data.camera, data.shake);
		}

		public virtual Transform playerTransform { get; set; }
		public virtual List<Vector3> focusPoints { get; set; }

		public virtual void SetPosition(ref CameraData data, Vector3 value, bool immediate = false)
		{
			_data = data;
			SetPosition(value, immediate);
		}

		protected virtual void SetPosition(Vector3 value, bool immediate = false)
		{
			_data.position = value;
			if (immediate) _data.lerpedPosition = value;
		}

		public virtual void SetTarget(ref CameraData data, Vector3 value, bool immediate = false)
		{
			_data = data;
			SetTarget(value, immediate);
		}

		protected virtual void SetTarget(Vector3 value, bool immediate = false)
		{
			_data.target = value;
			if (immediate) _data.lerpedTarget = value;
		}

		private bool HasStarted { get; set; }
		public virtual bool HasCompleted => false;
	}
}