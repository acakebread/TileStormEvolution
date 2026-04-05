using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameCameraEditor : CameraBase
	{
		public GameCameraEditor(Camera camera) : base(camera) { }

		public override void OnEnable()
		{
			EnablePostProcessing = PostProcessingEnabled;
			camera.fieldOfView = 60f;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}

		public override void Update()
		{
			iorigin = camera.transform.position;
			itarget = iorigin + camera.transform.forward;
		}

		public override void CopyFrom(CameraBase other) { }//do not copy position from game cameras - leave everything intact

		public override void OnMapOriginShift(Vector3 delta)
		{
			base.OnMapOriginShift(delta);
			camera.transform.position += delta; // move physical camera
		}
	}
}