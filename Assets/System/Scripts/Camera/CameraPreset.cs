using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		private const float SmoothingN = 32f;

		public override void Start() { }

		public override bool Update()
		{
			cameraData.smoothing = SmoothingUtils.Smooth(cameraData.smoothing, SmoothingN, Time.deltaTime, CameraData.TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, cameraData.smoothing, Time.deltaTime, CameraData.TargetFPS);
			cameraData.originSrc = Vector3.Lerp(cameraData.originSrc, cameraData.originDst, presetLerp);
			cameraData.targetSrc = Vector3.Lerp(cameraData.targetSrc, cameraData.targetDst, presetLerp);
			return true;
		}
	}
}
