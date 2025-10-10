namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		protected override void Awake()
		{
			data.fieldOfView = 45f;
			data.enablePostProcessing = false;
			data.camera.fieldOfView = data.fieldOfView;
		}

		protected override void Update()
		{
		}

		protected override void ApplyProjection() 
		{ 
		}
	}
}