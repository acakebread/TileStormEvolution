using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraAnimationData
	{
		public Vector3 lerpedPosition; // Maps to originSrc
		public Vector3 position;       // Maps to originDst
		public Vector3 lerpedTarget;   // Maps to targetSrc
		public Vector3 target;         // Maps to targetDst
		public float smoothing;
		public float fieldOfView;
		public float shake;
		public bool enablePostProcessing;
		public Camera camera;
		public PostProcessingCameraController postProcessingCameraController;

		public CameraAnimationData(Camera camera)
		{
			this.camera = camera;
			lerpedPosition = camera != null ? camera.transform.position : Vector3.zero;
			position = lerpedPosition;
			lerpedTarget = Vector3.zero;
			target = Vector3.zero;
			smoothing = CameraData.DefaultSmoothingRate;
			fieldOfView = camera != null ? camera.fieldOfView : 45f;
			shake = 0f;
			enablePostProcessing = true;
			postProcessingCameraController = null;
		}

		public void CopyFrom(CameraData source)
		{
			if (source == null) return;
			target = source.target;
			lerpedTarget = source.target; // Map CameraData.target to both target and lerpedTarget
			smoothing = source.smoothing;
			fieldOfView = source.fieldOfView;
			shake = source.shake;
			enablePostProcessing = source.enablePostProcessing;
		}

		public void CopyFrom(CameraAnimationData source)
		{
			if (source == null) return;
			position = source.position;
			lerpedPosition = source.lerpedPosition;
			fieldOfView = source.fieldOfView;
			smoothing = source.smoothing;
			shake = source.shake;
			target = source.target;
			lerpedTarget = source.lerpedTarget;
			postProcessingCameraController = source.postProcessingCameraController;
		}
	}
}