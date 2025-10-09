using UnityEngine;

namespace MassiveHadronLtd
{
	public struct CameraData
	{
		public const float TargetFPS = 60f;
		public const float DefaultSmoothingRate = 64f;

		public Camera camera;
		public Vector3 origin;
		public Vector3 lerpedOrigin;
		public Vector3 target;
		public Vector3 lerpedTarget;
		public float fieldOfView;
		public float shake;
		public float smoothing;
		public bool enablePostProcessing;
		public PostProcessingCameraController postProcessingCameraController;

		public CameraData(Camera camera)
		{
			this.camera = camera;
			origin = lerpedOrigin = camera != null ? camera.transform.position : Vector3.zero;
			target = lerpedTarget = Vector3.zero;
			fieldOfView = camera != null ? camera.fieldOfView : 60f;
			shake = 0f;
			smoothing = DefaultSmoothingRate;
			enablePostProcessing = false;
			postProcessingCameraController = null;
		}
	}
}
