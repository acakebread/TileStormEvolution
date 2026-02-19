using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameCameraEditor : CameraBase
	{
		public GameCameraEditor(Camera camera) : base(camera) { }

		public override void OnEnable()
		{
			camera.fieldOfView = 60f;
			//postProcessingEnabled = false;
			EnablePostProcessing = PostProcessingEnabled;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}

		public override void OnMapOriginShift(Vector3 delta)
		{
			base.OnMapOriginShift(delta);
			camera.transform.position += delta; // move physical camera
		}
	}
}