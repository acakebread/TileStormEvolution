using UnityEngine;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Light))]
	public class DirectionalLightUtility : MonoBehaviour
	{
		[Header("Lighting Settings")]
		[Range(0.005f, 1.0f)]
		[SerializeField] private float threshold = 0.85f;

		[Range(1f, 20f)]
		[SerializeField] private float intensityMultiplier = 1f;

		[Range(0f, 3f)]
		[SerializeField] private float minIntensity = 0.2f;

		private Light directionalLight => GetComponent<Light>();

		public static DirectionalLightUtility Instantiate(Transform parent = null)
		{
			var instance = new GameObject("DirectionalLight", typeof(DirectionalLightUtility));
			if (parent != null)
				instance.transform.parent = parent;

			var util = instance.GetComponent<DirectionalLightUtility>();
			var light = util.GetComponent<Light>();

			light.lightmapBakeType = LightmapBakeType.Baked;
			light.type = LightType.Directional;
			light.shadows = LightShadows.Soft;

			return util;
		}

		private void Awake()
		{
			SkyboxUtility.OnSkyboxChanged += UpdateFromSkybox;
		}

		private void OnDestroy()
		{
			SkyboxUtility.OnSkyboxChanged -= UpdateFromSkybox;
		}

		public void UpdateFromSkybox(Material skybox = null)
		{
			Cubemap cubemap = SkyboxUtility.GetTintedSkyboxCubemap(skybox);

			if (cubemap == null)
			{
				directionalLight.color = Color.white;
				directionalLight.intensity = 1f;
				return;
			}

			// Bright color using your existing utility
			Color brightColor = CubemapUtility.ComputeBrightColor(cubemap, threshold);

			// Light direction
			Vector3 lightDir = AtlasCubemapUtility.FindLightDirection(cubemap);
			//Vector3 lightDir = EquirectangularCubemapUtility.FindSunDirection(cubemap);

			// Apply to light
			directionalLight.color = brightColor;

			float lum = brightColor.Luminance();
			directionalLight.intensity = Mathf.Max(minIntensity, lum * intensityMultiplier);

			// Apply direction
			if (lightDir.sqrMagnitude > 0.001f)
			{
				transform.rotation = Quaternion.LookRotation(lightDir.normalized);
			}
			else
			{
				transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, -1f, 0.5f).normalized);
				Debug.LogWarning("No light direction determined - resorting to default");
			}

			// Optional production log (remove or wrap in #if UNITY_EDITOR later)
			Debug.Log($"Sky updated: BrightColor={brightColor}, Lum={lum:F3}, Intensity={directionalLight.intensity:F2}");
		}
	}
}