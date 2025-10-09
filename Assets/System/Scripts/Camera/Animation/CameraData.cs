using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraData
	{
		public const float TargetFPS = 60f;
		public const float DefaultSmoothingRate = 64f;

		public Camera camera;
		public Vector3 lerpedPosition;
		public Vector3 position;
		public Vector3 target;
		public Vector3 lerpedTarget;
		public float fieldOfView;
		public float shake;
		public bool enablePostProcessing;
		public PostProcessingCameraController postProcessingCameraController;
		public float smoothing;

		public CameraData(Camera camera)
		{
			this.camera = camera;
			position = lerpedPosition = camera != null ? camera.transform.position : Vector3.zero;
			target = lerpedTarget = Vector3.zero;
			fieldOfView = camera != null ? camera.fieldOfView : 60f;
			shake = 0f;
			enablePostProcessing = false;
			smoothing = DefaultSmoothingRate;
		}

		public void CopyFrom(CameraData source)
		{
			if (source == null) return;
			position = source.position;
			lerpedPosition = source.lerpedPosition;
			target = source.target;
			lerpedTarget = source.lerpedTarget;
			smoothing = source.smoothing;
			fieldOfView = source.fieldOfView;
			shake = source.shake;
			enablePostProcessing = source.enablePostProcessing;
			postProcessingCameraController = source.postProcessingCameraController;
		}
	}
}