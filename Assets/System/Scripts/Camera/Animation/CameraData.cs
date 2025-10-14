using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraData
	{
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
			fieldOfView = null != camera ? camera.fieldOfView : 60f;// Default Field of View
			shake = 0f;
			smoothing = 64f;// Default Smoothing Rate
			postProcessingEnabled = true;
		}

		private PostProcessingCameraController controller => camera.GetComponentInChildren<PostProcessingCameraController>(true);
	}
}
