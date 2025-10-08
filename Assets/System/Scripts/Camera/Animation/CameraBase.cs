using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected CameraAnimationData _data;

		public virtual void Start(ref CameraAnimationData data)
		{
			_data = data;
			HasStarted = true;
			Start();
		}

		protected virtual void Start() { }

		public void Update(ref CameraAnimationData data)
		{
			_data = data;
			if (!HasStarted) Start(ref _data);
			Update();
			ApplyProjection(_data);
		}

		protected virtual void Update() { }

		protected virtual void ApplyProjection(CameraAnimationData data)
		{
			if (data.camera == null) return;
			var direction = data.lerpedTarget - data.camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
			CameraUtils.ApplyCameraShake(data.camera, data.shake);
			if (data.postProcessingCameraController != null)
				data.postProcessingCameraController.enabled = data.enablePostProcessing;
		}

		public virtual Transform playerTransform { get; set; }
		public virtual List<Vector3> focusPoints { get; set; }

		public virtual void SetPosition(ref CameraAnimationData data, Vector3 value, bool immediate = false)
		{
			_data = data;
			SetPosition(value, immediate);
		}

		protected virtual void SetPosition(Vector3 value, bool immediate = false)
		{
			_data.position = value;
			if (immediate) _data.camera.transform.position = value;
		}

		public virtual void SetTarget(ref CameraAnimationData data, Vector3 value, bool immediate = false)
		{
			_data = data;
			SetTarget(value, immediate);
		}

		protected virtual void SetTarget(Vector3 value, bool immediate = false)
		{
			_data.target = value;
			if (immediate) _data.lerpedTarget = value;
		}

		public bool HasStarted { private get; set; }
		public virtual bool HasCompleted => false;
	}
}