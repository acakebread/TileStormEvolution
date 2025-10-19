using System;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		public Func<Vector3> originFn { get; set; }
		public Func<Vector3> targetFn { get; set; }

		private Vector3 origin;
		private Vector3 target;

		private const float SmoothingN = 32f;

		public CameraPreset(Camera camera) : base(camera) { }

		public override void Start()
		{
			base.Start();
			fieldOfView = 20f;
			origin = originFn?.Invoke() ?? Vector3.zero;
			target = targetFn?.Invoke() ?? Vector3.forward;
			postProcessingEnabled = true;
		}

		public override void Update()
		{
			base.Update();
			smoothing = SmoothingUtils.Smooth(smoothing, SmoothingN, Time.deltaTime, TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, TargetFPS);
			iorigin = Vector3.Lerp(iorigin, origin, presetLerp);
			itarget = Vector3.Lerp(itarget, target, presetLerp);
			OnRender();
		}

		protected override void OnRender()
		{
			if (camera == null) return;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
		}
	}
}