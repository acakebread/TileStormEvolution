using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		private static class FollowConfig
		{
			public const float SmoothingNa = 8f;
			public const float SmoothingNb = 64f;
			public const float IdealDistance = 14f;
			public const float IdealDistanceHorizontalScale = 1.4f;
		}

		public override void Start()
		{
			//cameraData = CameraController.defaultCameraData;
		}

		public override bool Update()
		{
			cameraData.smoothing = SmoothingUtils.Smooth(cameraData.smoothing, FollowConfig.SmoothingNa, FollowConfig.SmoothingNb, Time.deltaTime, CameraData.TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, cameraData.smoothing, Time.deltaTime, CameraData.TargetFPS);
			cameraData.targetSrc = Vector3.Lerp(cameraData.targetSrc, cameraData.targetDst, followLerp);
			var delta = cameraData.targetSrc - cameraData.originSrc;
			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? CameraController.mainCamera.transform.forward : new Vector3(delta.x, 0, delta.z);
			deltaHorizontal.Normalize();
			var idealPos = cameraData.targetSrc - deltaHorizontal * (FollowConfig.IdealDistance * FollowConfig.IdealDistanceHorizontalScale);
			idealPos.y = cameraData.targetSrc.y + FollowConfig.IdealDistance;
			cameraData.originSrc = Vector3.Lerp(cameraData.originSrc, idealPos, followLerp);
			return true;
		}
	}
}
