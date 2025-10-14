using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraState
	{
		public CameraMode mode;
		public CameraData data;
		public Func<Vector3> origin;//lerped origin
		public Func<Vector3> target;//lerped target
		public Func<IReadOnlyList<Vector3>> points;//focus points
	}
}