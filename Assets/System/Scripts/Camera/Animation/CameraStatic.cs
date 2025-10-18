using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraStatic : CameraBase
	{
		public CameraStatic(CameraConfig config) : base(config)
		{
			if (null != config)
				data = config.data;
			//initialise camera
			var camera = data.camera;
			if (null == camera)
				return;
			camera.transform.position = data.origin;
			var direction = data.target - camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = 60f;
		}
	}
}