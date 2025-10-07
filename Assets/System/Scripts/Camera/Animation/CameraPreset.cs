using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		private const float SmoothingN = 32f; 
		
		public override void Update(ref CameraData data)
		{
			base.Update(ref data);
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingN, Time.deltaTime, CameraData.TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.originSrc = Vector3.Lerp(data.originSrc, data.originDst, presetLerp);
			data.targetSrc = Vector3.Lerp(data.targetSrc, data.targetDst, presetLerp);
		}
	}
}