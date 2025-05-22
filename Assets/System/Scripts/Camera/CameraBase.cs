namespace MassiveHadronLtd
{
	public abstract class CameraBase
    {
		//public float smoothing;
		//public Vector3 originSrc;
		//public Vector3 originDst;
		//public Vector3 targetSrc;
		//public Vector3 targetDst;
		//public float fieldOfView;
		//public float shake;//deviation amplitude

		public CameraData cameraData;
		public abstract void Start();
		public abstract bool Update();
	}
}