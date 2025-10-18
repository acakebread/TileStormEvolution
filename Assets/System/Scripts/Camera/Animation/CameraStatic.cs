namespace MassiveHadronLtd
{
	public class CameraStatic : CameraBase
	{
		public CameraStatic(CameraConfig config) : base(config) { }

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 60f;
		}
	}
}