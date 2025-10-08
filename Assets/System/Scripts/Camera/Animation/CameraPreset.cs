using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraPreset : CameraBase
	{
		private const float SmoothingN = 32f;

		protected override void Update()
		{
			if (_data.camera == null) return;
			_data.smoothing = SmoothingUtils.Smooth(_data.smoothing, SmoothingN, Time.deltaTime, CameraData.TargetFPS);
			var presetLerp = SmoothingUtils.Smooth(0f, 1f, _data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			_data.lerpedTarget = Vector3.Lerp(_data.lerpedTarget, _data.target, presetLerp);
			_data.camera.transform.position = Vector3.Lerp(_data.camera.transform.position, _data.position, presetLerp);
		}
	}
}