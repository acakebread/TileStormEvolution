using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		public override void Start()
		{
			base.Start();
			cameraData.fieldOfView = 45f;
			cameraData.enablePostProcessing = false;
		}

		public override void Project(Camera camera = null)
		{
			camera ??= Camera.main;
			camera.fieldOfView = cameraData.fieldOfView;
		}
	}
}