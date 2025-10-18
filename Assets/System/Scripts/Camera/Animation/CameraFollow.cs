using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		protected Func<Vector3> originFn;
		protected Func<Vector3> targetFn;
		protected Func<IReadOnlyList<Vector3>> pointsFn;
		protected Vector3 origin => originFn?.Invoke() ?? Vector3.zero;
		protected Vector3 target => targetFn?.Invoke() ?? Vector3.zero;
		protected IReadOnlyList<Vector3> points => pointsFn?.Invoke() ?? Array.Empty<Vector3>();//focus points

		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;

		public CameraFollow(CameraConfig config) : base(config)
		{
			if (null != config)
			{
				data = config.data;
				originFn = config.origin;
				targetFn = config.target;
				pointsFn = config.points;
			}
		}

		public override void Awake()
		{
			//initialise camera
			var camera = data.camera;
			if (camera == null) return;
			camera.transform.position = originFn?.Invoke() ?? data.iorigin;
			var direction = (targetFn?.Invoke() ?? data.itarget) - camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 20f;
			smoothing = 64f;
		}

		public override void Update()
		{
			base.Update();
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
			if (data?.camera == null) return;
			data.camera.transform.position = data.iorigin;
			var direction = data.itarget - data.iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				data.camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			data.camera.fieldOfView = data.fieldOfView;
			//CameraUtils.ApplyCameraShake(data.camera, shake);
		}
	}
}