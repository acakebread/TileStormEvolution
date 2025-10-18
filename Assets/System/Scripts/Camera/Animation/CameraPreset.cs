using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		private const float SmoothingN = 32f;
		private Vector3 localOrigin; // Renamed to avoid conflict with helper property
		private Vector3 localTarget;

		public CameraPreset(CameraConfig config) : base(config) { }

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
			ApplyProjection();
		}
	}
}