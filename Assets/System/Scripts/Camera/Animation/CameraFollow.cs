using UnityEngine;
namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;
		public override void Update(ref CameraAnimationData data)
		{
			base.Update(ref data);
			if (data.camera == null || playerTransform == null) return;
			data.target = playerTransform.position;
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingNa, SmoothingNb, Time.deltaTime, CameraData.TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.lerpedTarget = Vector3.Lerp(data.lerpedTarget, data.target, followLerp);
			var delta = data.lerpedTarget - data.camera.transform.position;
			var deltaHorizontal = (delta.x == 0f && delta.z == 0f) ? Vector3.zero : new Vector3(delta.x, 0, delta.z).normalized;
			var idealPos = data.lerpedTarget - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
			idealPos.y = data.lerpedTarget.y + IdealDistance;
			data.camera.transform.position = Vector3.Lerp(data.camera.transform.position, idealPos, followLerp);
		}
		public override Transform playerTransform
		{
			get => base.playerTransform;
			set => base.playerTransform = value;
		}
	}
}