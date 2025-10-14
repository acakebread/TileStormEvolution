namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		public CameraEditor(CameraState state) : base(state) { }

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 45f;
			data.postProcessingEnabled = false;
			data.camera.fieldOfView = data.fieldOfView;
		}

		public override void Update()
		{
			base.Update();
		}

		protected override void ApplyProjection() { }
	}
}