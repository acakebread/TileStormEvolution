using UnityEngine;

namespace MassiveHadronLtd
{
	public static class EllipsoidRandom
	{
		// --------------------------------------------------------------
		// Public entry point
		// --------------------------------------------------------------
		public static Vector3 Inside(Vector3 semiAxes)               // (a,b,c)
		{
			Vector3 p1 = RandomOnSurface(semiAxes);
			Vector3 p2 = RandomOnSurface(semiAxes);

			// Random t in [0,1] → any point on the segment
			float t = Random.value;
			return Vector3.Lerp(p1, p2, t);
		}

		// --------------------------------------------------------------
		// Random point *on the surface* of the ellipsoid
		// --------------------------------------------------------------
		private static Vector3 RandomOnSurface(Vector3 semiAxes)
		{
			// 1. Uniform direction on the unit sphere (Gaussian method)
			Vector3 dir;
			do
			{
				dir = new Vector3(Gaussian(), Gaussian(), Gaussian());
			} while (dir.sqrMagnitude < 1e-12f);
			dir.Normalize();

			// 2. Scale by the semi-axes → point on the ellipsoid surface
			return Vector3.Scale(dir, semiAxes);
		}

		// --------------------------------------------------------------
		// Fast Box-Muller Gaussian (mean 0, std-dev 1)
		// --------------------------------------------------------------
		private static float Gaussian()
		{
			float u = Mathf.Max(1e-7f, 1f - Random.value); // Unity Random.value can very rarely be exactly 1.
			float v = Random.value;
			return Mathf.Sqrt(-2f * Mathf.Log(u)) * Mathf.Cos(Mathf.PI * 2f * v);
		}
	}
}
