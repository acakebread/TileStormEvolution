namespace MassiveHadronLtd
{
	public class CameraDirect : CameraBase
	{
		public CameraDirect(CameraState state) : base(state) { }

		public override void Start()
		{
			base.Start();
			//data.fieldOfView = 60f;
		}
	}
}