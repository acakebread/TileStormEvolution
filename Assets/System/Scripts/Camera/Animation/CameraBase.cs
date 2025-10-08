using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		public virtual void Start(ref CameraAnimationData data)
		{
			HasStarted = true;
			if (data.postProcessingCameraController != null)
			{
				data.postProcessingCameraController.enabled = data.enablePostProcessing;
			}
		}
		public virtual void Update(ref CameraAnimationData data)
		{
			if (!HasStarted) Start(ref data);
			ApplyProjection(ref data);
		}
		protected virtual void ApplyProjection(ref CameraAnimationData data)
		{
			if (data.camera == null) return;
			var direction = data.lerpedTarget - data.camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
			CameraUtils.ApplyCameraShake(data.camera, data.shake);
		}

		public virtual Transform playerTransform { get; set; }
		public virtual List<Vector3> focusPoints { get; set; }
		public virtual void SetPosition(ref CameraAnimationData data, Vector3 value, bool immediate = false)
		{
			data.position = value;
			if (immediate) data.camera.transform.position = value;
		}
		public virtual void SetTarget(ref CameraAnimationData data, Vector3 value, bool immediate = false)
		{
			data.target = value;
			if (immediate) data.lerpedTarget = value;
		}
		public bool HasStarted { private get; set; }
		public virtual bool HasCompleted => false;
	}
}