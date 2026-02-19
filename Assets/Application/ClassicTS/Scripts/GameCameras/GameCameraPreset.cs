using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameCameraPreset : CameraBase
	{
		public Func<Vector3> originFn { get; set; }
		public Func<Vector3> targetFn { get; set; }

		private Vector3 origin;
		private Vector3 target;

		private const float SmoothingN = 32f;

		public GameCameraPreset(Camera camera) : base(camera) { }

		public override void OnEnable()
		{
			base.OnEnable();
			fieldOfView = 20f;
			origin = originFn?.Invoke() ?? Vector3.zero;
			target = targetFn?.Invoke() ?? Vector3.forward;
			EnablePostProcessing = PostProcessingEnabled;
			//postProcessingEnabled = true;
		}

		public override void Update()
		{
			base.Update();
			smoothing = SmoothingUtils.Smooth(smoothing, SmoothingN, Time.deltaTime, TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, TargetFPS);
			iorigin = Vector3.Lerp(iorigin, origin, presetLerp);
			itarget = Vector3.Lerp(itarget, target, presetLerp);

			if (camera == null) return;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
		}

		public override void OnMapOriginShift(Vector3 delta)
		{
			base.OnMapOriginShift(delta);
			origin += delta;
			target += delta;
		}
	}
}