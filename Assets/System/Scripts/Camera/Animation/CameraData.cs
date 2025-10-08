using UnityEngine;

namespace MassiveHadronLtd
{
	public struct CameraData
	{
		public Camera camera; // Reference to the main camera
		public Camera postProcessingCamera; // Reference to post-processing camera (optional)
		public float smoothing;
		public Vector3 position; // Desired camera position (replaces originDst)
		public Vector3 target; // Desired look-at target (replaces targetDst)
		public Vector3 lerpedTarget; // Current lerped look-at target (replaces targetSrc)
		public float fieldOfView;
		public float shake; // Deviation amplitude
		public bool enablePostProcessing;
		public const float TargetFPS = 60f;
		public const float DefaultSmoothingRate = 64f;
	}
}