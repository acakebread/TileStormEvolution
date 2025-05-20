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
	}
}