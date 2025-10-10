namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		public override void Awake()
		{
			base.Awake();
			data.fieldOfView = 45f;
			data.postProcessingEnabled = false;
			data.camera.fieldOfView = data.fieldOfView;
		}

		public override void Update()
		{
			base.Update();
		}

		protected override void ApplyProjection() 
		{ 
		}
	}
}