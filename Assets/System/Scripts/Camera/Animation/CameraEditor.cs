using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		public override void Start(ref CameraData data)
		{
			base.Start(ref data);
			data.fieldOfView = 45f;
			data.enablePostProcessing = false;
		}

		public override void Project(ref CameraData data)
		{
			if (data.camera == null) return;
			data.camera.fieldOfView = data.fieldOfView;
		}
	}
}