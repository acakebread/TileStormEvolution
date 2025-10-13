using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 20f;
			data.smoothing = 64f;
		}

		public override void Update()
		{
			base.Update();
			var target = targetFunc.Invoke();
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingNa, SmoothingNb, Time.deltaTime, CameraData.TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.target = Vector3.Lerp(data.target, target, followLerp);
			var delta = data.target - data.origin;
			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? Vector3.zero : new Vector3(delta.x, 0, delta.z).normalized;
			var origin = data.target - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
			origin.y = data.target.y + IdealDistance;
			data.origin = Vector3.Lerp(data.origin, origin, followLerp);
			ApplyProjection();
		}
	}
}
