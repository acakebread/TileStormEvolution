using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraStatic : CameraBase
	{
		public override void SetOrigin(Vector3 value)
		{
			base.SetOrigin(value);
			cameraData.originSrc = value;
		}

		public override void SetTarget(Vector3 value)
		{
			base.SetOrigin(value);
			cameraData.targetSrc = value;
		}
	}
}
