// CameraStateSnapshot.cs
using UnityEngine;

namespace MassiveHadronLtd
{
	public struct CameraStateSnapshot
	{
		public Vector3 position; // Camera's current position
		public Vector3 target; // Desired target
		public Vector3 lerpedTarget; // Current lerped target
		public float smoothing;
		public float fieldOfView;
		public float shake;
		public bool enablePostProcessing;
	}
}