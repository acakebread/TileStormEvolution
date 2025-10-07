using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;
		
		public override void Update(ref CameraData data)
		{
			base.Update(ref data);
			data.targetDst = playerTransform.position;
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingNa, SmoothingNb, Time.deltaTime, CameraData.TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.targetSrc = Vector3.Lerp(data.targetSrc, data.targetDst, followLerp);
			var delta = data.targetSrc - data.originSrc;
			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? Vector3.zero : new Vector3(delta.x, 0, delta.z).normalized;
			var idealPos = data.targetSrc - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
			idealPos.y = data.targetSrc.y + IdealDistance;
			data.originSrc = Vector3.Lerp(data.originSrc, idealPos, followLerp);
		}

		public override Transform playerTransform
		{
			get => base.playerTransform;
			set => base.playerTransform = value;
		}
	}
}