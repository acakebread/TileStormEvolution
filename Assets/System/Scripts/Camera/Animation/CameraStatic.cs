namespace MassiveHadronLtd
{
	public class CameraStatic : CameraBase
	{
		public override void Start()
		{
			base.Start();
			data.fieldOfView = 60f;
		}
	}
}
