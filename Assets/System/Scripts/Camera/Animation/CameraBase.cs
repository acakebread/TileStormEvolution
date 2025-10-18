namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected const float TargetFPS = 60f;

		protected CameraData data;
		public CameraData Data { get => data; set => data = value; }

		public CameraBase(CameraConfig config) { }

		public virtual void Awake() { }
		public virtual void Start() { }
		public virtual void Update() { }
		public virtual void OnApplicationFocus(bool hasFocus) { }
		protected virtual void OnRender() { }
	}
}