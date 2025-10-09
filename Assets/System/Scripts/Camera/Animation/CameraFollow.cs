using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;

		protected override void Update()
		{
			playerTransform = OnUpdatePlayer?.Invoke();
			if (playerTransform == null) return;

			_data.target = playerTransform.position;
			_data.smoothing = SmoothingUtils.Smooth(_data.smoothing, SmoothingNa, SmoothingNb, Time.deltaTime, CameraData.TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, _data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			_data.lerpedTarget = Vector3.Lerp(_data.lerpedTarget, _data.target, followLerp);
			var delta = _data.lerpedTarget - _data.lerpedOrigin;
			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? Vector3.zero : new Vector3(delta.x, 0, delta.z).normalized;
			var idealPos = _data.lerpedTarget - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
			idealPos.y = _data.lerpedTarget.y + IdealDistance;
			_data.origin = idealPos;
			_data.lerpedOrigin = Vector3.Lerp(_data.lerpedOrigin, _data.origin, followLerp);
		}

		//public override Transform playerTransform
		//{
		//	get => base.playerTransform;
		//	set => base.playerTransform = value;
		//}
	}
}