namespace MassiveHadronLtd
{
	public class CameraStatic : CameraBase
	{
		public override void Awake()
		{
			base.Awake();
			data.fieldOfView = 20f;
		}
	}
}
