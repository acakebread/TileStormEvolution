namespace MassiveHadronLtd
{
	public class CameraDirect : CameraBase
	{
		public CameraDirect(CameraConfig config) : base(config) { }

		public override void Start()
		{
			base.Start();
			//data.fieldOfView = 60f;
		}
	}
}