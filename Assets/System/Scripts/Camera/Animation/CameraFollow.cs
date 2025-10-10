using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;

		public override void Awake()
		{
			base.Awake();
			data.fieldOfView = 20f;
		}

		public override void Update()
		{
			base.Update();
			var playerTransform = base.playerTransform?.Invoke();
			if (playerTransform == null) return;

			data.target = playerTransform.position;
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingNa, SmoothingNb, Time.deltaTime, CameraData.TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.lerpedTarget = Vector3.Lerp(data.lerpedTarget, data.target, followLerp);
			var delta = data.lerpedTarget - data.lerpedOrigin;
			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? Vector3.zero : new Vector3(delta.x, 0, delta.z).normalized;
			var idealPos = data.lerpedTarget - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
			idealPos.y = data.lerpedTarget.y + IdealDistance;
			data.origin = idealPos;
			data.lerpedOrigin = Vector3.Lerp(data.lerpedOrigin, data.origin, followLerp);
			ApplyProjection();
		}
	}
}