using UnityEngine;

namespace MassiveHadronLtd
{
	public static class FovHelper
	{
		private const float FovMin = 35f;
		private const float FovMax = 55f;

		public static float InitializeFovMax()
		{
			return Random.value < 0.2f ? 60f : FovMax;
		}

		public static float UpdateFov(float sequenceTimer, float sequenceDuration)
		{
			if (sequenceDuration <= 0f) return FovMin;
			float fovT = SmoothingUtils.EasePingPong(sequenceTimer / sequenceDuration);
			return Mathf.Lerp(FovMin, InitializeFovMax(), fovT);
		}
	}
}