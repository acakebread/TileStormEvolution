using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameCameraStatic : CameraBase
	{
		public GameCameraStatic(Camera camera) : base(camera)
		{
			//initialise camera
			if (null == camera) return;
			camera.transform.position = iorigin;
			var direction = itarget - camera.transform.position;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = 60f;
		}
	}
}