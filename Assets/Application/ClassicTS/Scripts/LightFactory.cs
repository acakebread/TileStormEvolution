using UnityEngine;

namespace ClassicTilestorm
{
	public static class LightFactory
	{
		public static Light AddPointLight(GameObject gameObject, Color color, float intensity = 1f, float range = 1f)
		{
			var existingLight = gameObject.GetComponent<Light>();
			if (null != existingLight)
			{
				Debug.Log($"{gameObject.name} already has light of type {existingLight.type}");
				return existingLight;
			}

			var pointLight = gameObject.AddComponent<Light>();
			pointLight.type = LightType.Point;
			pointLight.color = color;
			pointLight.intensity = intensity;
			pointLight.range = range;
			pointLight.shadows = LightShadows.None;
			return pointLight;
		}
	}
}