namespace MassiveHadronLtd
{
	public class CameraStatic : CameraBase
	{
		public CameraStatic(CameraState state) : base(state) { }

		public override void Start()
		{
			base.Start();
			data.fieldOfView = 60f;
		}
	}
}