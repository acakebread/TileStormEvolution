using UnityEngine;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		protected Camera camera;
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
		public virtual void Update() { }
		public virtual void OnApplicationFocus(bool hasFocus) { }
		protected virtual void OnRender() { }

		public virtual void CopyFrom(CameraBase other)
		{
			if (other == null) return;
			iorigin = other.iorigin;
			itarget = other.itarget;
			smoothing = other.smoothing;
		}

		protected float smoothing = 64f;// Default Smoothing Rate
		protected const float TargetFPS = 60f;

		protected bool postProcessingEnabled
		{
			get => null != controller ? controller.enabled : false;
			set { if (null != controller) controller.enabled = value; }
		}
		private PostProcessingCameraController controller => camera?.GetComponentInChildren<PostProcessingCameraController>(true);
	}
}