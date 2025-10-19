using System;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraConfig
	{
		public CameraData data;
		public Func<Vector3> origin;
		public Func<Vector3> target;
	}
}