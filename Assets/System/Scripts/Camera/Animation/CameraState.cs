using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraState
	{
		public CameraMode cameraMode;
		public CameraData data;
		public Func<Vector3> origin;
		public Func<Vector3> target;
		public Func<IReadOnlyList<Vector3>> focusPoints;
	}
}