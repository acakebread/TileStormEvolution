namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		protected override void Start()
		{
			base.Start();
			_data.camera.fieldOfView = 45;
			_data.enablePostProcessing = false;
		}

		protected override void ApplyProjection() { }// (CameraData data) {}//skip
	}
}