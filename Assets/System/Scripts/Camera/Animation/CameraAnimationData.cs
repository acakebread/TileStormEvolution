using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraAnimationData
	{
		public Vector3 position;
		public Vector3 target;
		public Vector3 lerpedTarget;
		public float smoothing;
		public float fieldOfView;
		public float shake;
		public bool enablePostProcessing;
		public Camera camera;
		public PostProcessingCameraController postProcessingCameraController;

		public CameraAnimationData(Camera camera)
		{
			this.camera = camera;
			position = camera != null ? camera.transform.position : Vector3.zero;
			target = Vector3.zero;
			lerpedTarget = Vector3.zero;
			smoothing = CameraData.DefaultSmoothingRate;
			fieldOfView = camera != null ? camera.fieldOfView : 60f;
			shake = 0f;
			enablePostProcessing = false;
			postProcessingCameraController = null;
		}

		public void CopyFrom(CameraData source)
		{
			if (source == null) return;
			target = source.target;
			smoothing = source.smoothing;
			fieldOfView = source.fieldOfView;
			shake = source.shake;
			enablePostProcessing = source.enablePostProcessing;
		}

		public void CopyFrom(CameraAnimationData source)
		{
			if (source == null) return;
			position = source.position;
			lerpedTarget = source.lerpedTarget;
			postProcessingCameraController = source.postProcessingCameraController;
		}
	}
}