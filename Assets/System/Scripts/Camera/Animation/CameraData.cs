using UnityEngine;

namespace MassiveHadronLtd
{
	public struct CameraData
	{
		public float smoothing;
		public Vector3 originSrc;
		public Vector3 originDst;
		public Vector3 targetSrc;
		public Vector3 targetDst;
		public float fieldOfView;
		public float shake;//deviation amplitude
		public bool enablePostProcessing;

		public const float TargetFPS = 60f;
		public const float DefaultSmoothingRate = 64f;
	}
}