using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraStatic : CameraBase
	{
		public CameraStatic(CameraData _data) : base(_data)
		{
			data = _data;//noty sure if data will already be valid from base
			//initialise camera
			var camera = data.camera;
			if (null == camera)
				return;
			camera.transform.position = data.iorigin;
			var direction = data.itarget - camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = 60f;
		}
	}
}