using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraData
	{
		public const float DefaultSmoothingRate = 8f;
		public const float TargetFPS = 60f;

		public Vector3 target;
		public float smoothing;
		public float fieldOfView;
		public float shake;
		public bool enablePostProcessing;

		public CameraData(Camera camera)
		{
			smoothing = DefaultSmoothingRate;
			fieldOfView = camera != null ? camera.fieldOfView : 60f;
			shake = 0f;
			enablePostProcessing = false;
			target = Vector3.zero;
		}

		public void CopyFrom(CameraAnimationData source)
		{
			if (source == null) return;
			target = source.target;
			smoothing = source.smoothing;
			fieldOfView = source.fieldOfView;
			shake = source.shake;
			enablePostProcessing = source.enablePostProcessing;
		}
	}
}