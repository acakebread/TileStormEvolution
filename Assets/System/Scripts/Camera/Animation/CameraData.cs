using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraData
	{
		public Vector3 target; // Desired look-at target
		public float smoothing;
		public float fieldOfView;
		public float shake; // Deviation amplitude
		public bool enablePostProcessing;
		public const float TargetFPS = 60f;
		public const float DefaultSmoothingRate = 64f;

		public CameraData(Camera cam = null)
		{
			target = Vector3.zero;
			smoothing = DefaultSmoothingRate;
			fieldOfView = cam != null ? cam.fieldOfView : 45f;
			shake = 0f;
			enablePostProcessing = true;
		}
	}
}