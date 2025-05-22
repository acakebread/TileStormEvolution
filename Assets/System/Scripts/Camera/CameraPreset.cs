using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		private static class PresetConfig
		{
			public const float SmoothingN = 32f;
		}

		public override void Start()
		{
			//cameraData = CameraController.defaultCameraData;
		}

		public override bool Update()
		{
			cameraData.smoothing = SmoothingUtils.Smooth(cameraData.smoothing, PresetConfig.SmoothingN, Time.deltaTime, CameraData.TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, cameraData.smoothing, Time.deltaTime, CameraData.TargetFPS);
			cameraData.originSrc = Vector3.Lerp(cameraData.originSrc, cameraData.originDst, presetLerp);
			cameraData.targetSrc = Vector3.Lerp(cameraData.targetSrc, cameraData.targetDst, presetLerp);
			return true;
		}
	}
}
