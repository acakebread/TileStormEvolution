using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraData
	{
		public const float TargetFPS = 60f;
		public const float DefaultSmoothingRate = 64f;

		public Camera camera;
		public Vector3 origin;
		public Vector3 target;
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

			origin = null != camera ? camera.transform.position : Vector3.zero;
			target = origin + Vector3.forward;
			fieldOfView = null != camera ? camera.fieldOfView : 60f;
			shake = 0f;
			smoothing = DefaultSmoothingRate;
			postProcessingEnabled = true;
		}

		private PostProcessingCameraController controller => camera.GetComponentInChildren<PostProcessingCameraController>(true);
	}
}
