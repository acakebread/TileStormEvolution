using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		public override void Start(ref CameraAnimationData data)
		{
			base.Start(ref data);
			data.fieldOfView = 45f;
			data.enablePostProcessing = false;
		}

		protected override void ApplyProjection(CameraAnimationData data)
		{
			if (data.camera == null) return;
			data.camera.fieldOfView = data.fieldOfView;
			if (data.postProcessingCameraController != null)
				data.postProcessingCameraController.enabled = data.enablePostProcessing;
		}
	}
}