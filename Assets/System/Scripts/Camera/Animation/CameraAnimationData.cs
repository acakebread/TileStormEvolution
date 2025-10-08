using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraAnimationData : CameraData
	{
		public Camera camera; // Reference to the main camera
		public PostProcessingCameraController postProcessingCameraController; // Reference to the post-processing camera controller (optional)
		public Vector3 position; // Camera's current position
		public Vector3 lerpedTarget; // Current lerped look-at target

		public CameraAnimationData(Camera cam = null) : base(cam)
		{
			camera = cam;
			postProcessingCameraController = null;
			position = Vector3.zero;
			lerpedTarget = Vector3.zero;
		}
	}
}