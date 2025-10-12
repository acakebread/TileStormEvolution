using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		private const float SmoothingN = 32f;

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 20f;
			data.origin = origin.Invoke();
			data.target = target.Invoke();
		}

		public override void Update()
		{
			base.Update();
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingN, Time.deltaTime, CameraData.TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.lerpedOrigin = Vector3.Lerp(data.lerpedOrigin, data.origin, presetLerp);
			data.lerpedTarget = Vector3.Lerp(data.lerpedTarget, data.target, presetLerp);
			ApplyProjection();
		}
	}
}