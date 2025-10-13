using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		private const float SmoothingN = 32f;
		private Vector3 origin;
		private Vector3 target;

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 20f;

			origin = originFunc.Invoke();
			target = targetFunc.Invoke();
		}
		
		public override void Update()
		{
			base.Update();
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingN, Time.deltaTime, CameraData.TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.origin = Vector3.Lerp(data.origin, origin, presetLerp);
			data.target = Vector3.Lerp(data.target, target, presetLerp);
			ApplyProjection();
		}
	}
}