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

		public CameraPreset(CameraData _data) : base(_data) { }

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 20f;
			origin = originFn?.Invoke() ?? Vector3.zero;
			target = targetFn?.Invoke() ?? Vector3.forward;
			postProcessingEnabled = true;
		}

		public override void Update()
		{
			base.Update();
			smoothing = SmoothingUtils.Smooth(smoothing, SmoothingN, Time.deltaTime, TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, TargetFPS);
			data.iorigin = Vector3.Lerp(data.iorigin, origin, presetLerp);
			data.itarget = Vector3.Lerp(data.itarget, target, presetLerp);
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
		}
	}
}