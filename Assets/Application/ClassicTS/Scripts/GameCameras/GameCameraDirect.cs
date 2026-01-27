using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	//direct control camera - all properties handled externally
	public class GameCameraDirect : CameraBase
	{
		public GameCameraDirect(Camera camera) : base(camera) { }

		public override void OnMapOriginShift(Vector3 delta)
		{
			base.OnMapOriginShift(delta);
		}
	}
}