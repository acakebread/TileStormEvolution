//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public class CameraDirect : CameraBase
//	{
//		public CameraDirect(CameraData _data) : base(_data)
//		{
//			if (null != config)
//				data = config.data;
//			//initialise camera
//			var camera = data.camera;
//			if (null == camera)
//				return;
//			camera.transform.position = data.iorigin;
//			var direction = data.itarget - camera.transform.position;
//			if (direction.sqrMagnitude > Mathf.Epsilon)
//				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
//		}
//	}
//}