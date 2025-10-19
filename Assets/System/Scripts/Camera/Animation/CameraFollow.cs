using System;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		public Func<Vector3> targetFn;
		
		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;

		public CameraFollow(CameraConfig config) : base(config)
		{
			data = config.data;
		}

		public override void Start()
		{
			base.Start();
			smoothing = 64f;
			if (null == data) return;
			data.fieldOfView = 20f;
		}

		public override void Update()
		{
			base.Update();
			var target = targetFn?.Invoke() ?? Vector3.forward;
			smoothing = SmoothingUtils.Smooth(smoothing, SmoothingNa, SmoothingNb, Time.deltaTime, TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, TargetFPS);
			data.itarget = Vector3.Lerp(data.itarget, target, followLerp); // Use helper property 'target'
			var delta = data.itarget - data.iorigin;
			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? Vector3.zero : new Vector3(delta.x, 0, delta.z).normalized;
			var origin = data.itarget - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
			origin.y = data.itarget.y + IdealDistance;
			data.iorigin = Vector3.Lerp(data.iorigin, origin, followLerp);
			OnRender();
		}

		protected override void OnRender()
		{
			if (null == data?.camera) return;
			data.camera.transform.position = data.iorigin;
			var direction = data.itarget - data.iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
		}
	}
}