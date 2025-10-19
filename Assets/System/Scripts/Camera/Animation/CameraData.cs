using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraData
	{
		public Camera camera;
		public Vector3 iorigin;
		public Vector3 itarget;
		public float fieldOfView;

		public CameraData(Camera camera)
		{
			this.camera = camera;
			iorigin = null != camera ? camera.transform.position : Vector3.zero;
			itarget = iorigin + Vector3.forward;
			fieldOfView = null != camera ? camera.fieldOfView : 60f;
		}
	}
}
