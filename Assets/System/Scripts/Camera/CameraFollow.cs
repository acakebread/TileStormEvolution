using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraFollow : CameraBase
	{
		private const float SmoothingNa = 8f;
		private const float SmoothingNb = 64f;
		private const float IdealDistance = 14f;
		private const float IdealDistanceHorizontalScale = 1.4f;

		public override void Start() { }

		public override bool Update()
		{
			cameraData.smoothing = SmoothingUtils.Smooth(cameraData.smoothing, SmoothingNa, SmoothingNb, Time.deltaTime, CameraData.TargetFPS);
			var followLerp = SmoothingUtils.Smooth(0f, 1f, cameraData.smoothing, Time.deltaTime, CameraData.TargetFPS);
			cameraData.targetSrc = Vector3.Lerp(cameraData.targetSrc, cameraData.targetDst, followLerp);
			var delta = cameraData.targetSrc - cameraData.originSrc;
			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? cameraData.camera.transform.forward : new Vector3(delta.x, 0, delta.z);
			deltaHorizontal.Normalize();
			var idealPos = cameraData.targetSrc - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
			idealPos.y = cameraData.targetSrc.y + IdealDistance;
			cameraData.originSrc = Vector3.Lerp(cameraData.originSrc, idealPos, followLerp);
			return true;
		}
	}
}
