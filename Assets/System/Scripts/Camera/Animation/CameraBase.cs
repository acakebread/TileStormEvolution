namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected CameraData data;

		public CameraBase(CameraConfig config) { }

		public virtual void Awake() { }
		public virtual void Start() { }
		public virtual void Update() { }
		public virtual void OnApplicationFocus(bool hasFocus) { }
		protected virtual void OnRender() { }

		public virtual void CopyFrom(CameraBase other)
		{
			if (other == null) return;
			data.iorigin = other.data.iorigin;
			data.itarget = other.data.itarget;
			data.postProcessingEnabled = other.data.postProcessingEnabled;
			smoothing = other.smoothing;
		}

		protected float smoothing = 64f;// Default Smoothing Rate
		protected const float TargetFPS = 60f;
	}
}