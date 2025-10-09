using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraAnimationData : CameraData
	{
		public Vector3 lerpedPosition; // Maps to originSrc
		public Vector3 position;       // Maps to originDst
		public Vector3 lerpedTarget;   // Maps to targetSrc
		public Camera camera;
		public PostProcessingCameraController postProcessingCameraController;

		public CameraAnimationData(Camera camera) : base(camera)
		{
			this.camera = camera;
			lerpedPosition = camera != null ? camera.transform.position : Vector3.zero;
			position = lerpedPosition;
			lerpedTarget = Vector3.zero;
		}

		public void CopyFrom(CameraAnimationData source)
		{
			if (source == null) return;
			base.CopyFrom(source); // Copies target, smoothing, fieldOfView, shake, enablePostProcessing
			position = source.position;
			lerpedPosition = source.lerpedPosition;
			lerpedTarget = source.lerpedTarget;
			postProcessingCameraController = source.postProcessingCameraController;
		}
	}
}