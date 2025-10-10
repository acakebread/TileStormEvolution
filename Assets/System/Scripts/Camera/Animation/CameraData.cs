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
		public bool postProcessingEnabled
		{
			get => null != controller ? controller.enabled : false;
			set { if (null != controller) controller.enabled = value; }
		}

		public CameraData(Camera camera)
		{
			this.camera = camera;
			origin = lerpedOrigin = camera != null ? camera.transform.position : Vector3.zero;
			target = lerpedTarget = Vector3.zero;
			fieldOfView = camera != null ? camera.fieldOfView : 60f;
			shake = 0f;
			smoothing = DefaultSmoothingRate;
			postProcessingEnabled = true;
		}

		private readonly PostProcessingCameraController controller => camera.GetComponentInChildren<PostProcessingCameraController>(true);
	}
}
