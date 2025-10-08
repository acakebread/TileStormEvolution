using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraAnimationData : CameraData
	{
		public Camera camera; // Reference to the main camera
		public Camera postProcessingCamera; // Reference to post-processing camera (optional)
		public Vector3 position; // Camera's current position
		public Vector3 lerpedTarget; // Current lerped look-at target

		public CameraAnimationData(Camera cam = null) : base(cam)
		{
			camera = cam;
			postProcessingCamera = null;
			position = Vector3.zero;
			lerpedTarget = Vector3.zero;
		}
	}
}