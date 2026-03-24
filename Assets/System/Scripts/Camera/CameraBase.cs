using UnityEngine;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		public Camera camera;
		public Vector3 iorigin;
		public Vector3 itarget;
		public float fieldOfView;

		public CameraBase(Camera camera) 
		{ 
			this.camera = camera;
			iorigin = null != camera ? camera.transform.position : Vector3.back;
			itarget = iorigin + Vector3.forward;
			fieldOfView = null != camera ? camera.fieldOfView : 60f;
		}

		public virtual void Awake() { }
		public virtual void Start() { }
		public virtual void OnEnable() { }
		public virtual void Update() { }
		public virtual void OnGUI() { }
		public virtual void OnDisable() { }
		public virtual void OnApplicationFocus(bool hasFocus) { }
		public virtual void OnDestroy() { }

		public virtual void CopyFrom(CameraBase other)
		{
			if (other == null) return;
			iorigin = other.iorigin;
			itarget = other.itarget;
			smoothing = other.smoothing;
		}

		protected float smoothing = 64f;// Default Smoothing Rate
		protected const float TargetFPS = 60f;

		public bool postProcessingEnabled;
		public bool PostProcessingEnabled
		{
			get => postProcessingEnabled;
			set => postProcessingEnabled = value;
		}

		protected bool EnablePostProcessing { set { if (null != controller) controller.enabled = value; } }

		public PostProcessingCameraController controller => camera?.GetComponentInChildren<PostProcessingCameraController>(true);

		public virtual void OnMapOriginShift(Vector3 delta)
		{
			// Default: do nothing (for cameras that don't need adjustment)
			// Or shift iorigin/itarget if they exist
			iorigin += delta;
			itarget += delta;
		}
	}
}