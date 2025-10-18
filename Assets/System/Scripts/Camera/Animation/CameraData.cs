using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraData
	{
		public Camera camera;
		public Vector3 iorigin;
		public Vector3 itarget;
		public float fieldOfView;
		public bool postProcessingEnabled
		{
			get => null != controller ? controller.enabled : false;
			set { if (null != controller) controller.enabled = value; }
		}

		public CameraData(Camera camera)
		{
			this.camera = camera;

			iorigin = null != camera ? camera.transform.position : Vector3.zero;
			itarget = iorigin + Vector3.forward;
			fieldOfView = null != camera ? camera.fieldOfView : 60f;// Default Field of View
			postProcessingEnabled = true;
		}

		private PostProcessingCameraController controller => camera.GetComponentInChildren<PostProcessingCameraController>(true);
	}
}
