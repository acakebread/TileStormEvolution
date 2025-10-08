using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		public override void Start(ref CameraAnimationData data)
		{
			base.Start(ref data);
			data.fieldOfView = 45f;
			data.enablePostProcessing = false;
		}

		public override void Project(ref CameraAnimationData data)
		{
			if (data.camera == null) return;
			data.camera.fieldOfView = data.fieldOfView;
		}
	}
}