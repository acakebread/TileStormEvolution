using UnityEngine;
namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		private const float SmoothingN = 32f;
		public override void Update(ref CameraAnimationData data)
		{
			base.Update(ref data);
			if (data.camera == null) return;
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingN, Time.deltaTime, CameraData.TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.lerpedTarget = Vector3.Lerp(data.lerpedTarget, data.target, presetLerp);
			data.camera.transform.position = Vector3.Lerp(data.camera.transform.position, data.position, presetLerp);
		}
	}
}