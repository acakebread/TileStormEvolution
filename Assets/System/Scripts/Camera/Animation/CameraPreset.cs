using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		protected Func<Vector3> originFn;
		protected Func<Vector3> targetFn;
		protected Func<IReadOnlyList<Vector3>> pointsFn;
		protected Vector3 origin => originFn?.Invoke() ?? Vector3.zero;
		protected Vector3 target => targetFn?.Invoke() ?? Vector3.zero;
		protected IReadOnlyList<Vector3> points => pointsFn?.Invoke() ?? Array.Empty<Vector3>();//focus points

		private const float SmoothingN = 32f;
		private Vector3 localOrigin; // Renamed to avoid conflict with helper property
		private Vector3 localTarget;

		public Func<Vector3> OriginFn { set => originFn = value; }
		public Func<Vector3> TargetFn { set => targetFn = value; }

		public CameraPreset(CameraConfig config) : base(config)
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
			camera.transform.position = originFn?.Invoke() ?? data.origin;
			var direction = (targetFn?.Invoke() ?? data.target) - camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 20f;

			localOrigin = origin; // Use helper property 'origin'
			localTarget = target; // Use helper property 'target'
		}

		public override void Update()
		{
			base.Update();
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingN, Time.deltaTime, TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, TargetFPS);
			data.origin = Vector3.Lerp(data.origin, localOrigin, presetLerp);
			data.target = Vector3.Lerp(data.target, localTarget, presetLerp);
			OnRender();
		}

		protected override void OnRender()
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