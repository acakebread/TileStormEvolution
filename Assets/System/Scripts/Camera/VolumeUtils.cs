using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public static class VolumeUtils
	{
		//Bokeh focus distance compensation factor
		private static float focusDistanceMultiplier = 1f;
		public static void SetFocusDistanceMultiplier(float value) => focusDistanceMultiplier = value;

		private static DepthOfField getDepthOfField(Volume volume)
		{
			if (null == volume || null == volume.profile) return null;
			volume.profile.TryGet<DepthOfField>(out var result);
			return result;
		}

		public static void EnableDepthOfField(Volume volume, bool value)
		{
			var depthOfField = getDepthOfField(volume);
			if (null != depthOfField) depthOfField.active = value;
		}

		public static void SetDepthOfFieldDistance(Volume volume, float distance)
		{
			var depthOfField = getDepthOfField(volume);
			if (null != depthOfField) depthOfField.focusDistance.Override(distance * focusDistanceMultiplier);
		}
	}
}