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
		[SerializeField] private float intensityMultiplier = 1.2f;

		[Range(0f, 3f)]
		[SerializeField] private float minIntensity = 0f;

		private Light directionalLight => GetComponent<Light>();

		// Cache only for light direction (bright color is already cached efficiently in CubemapUtility)
		private Cubemap _lastCubemapForDirection;
		private Vector3 _cachedLightDirection = Vector3.zero;

		public static DirectionalLightUtility Instantiate(Transform parent = null)
		{
			var instance = new GameObject("DirectionalLight", typeof(DirectionalLightUtility));
			if (parent != null)
				instance.transform.parent = parent;

			var util = instance.GetComponent<DirectionalLightUtility>();
			var light = util.GetComponent<Light>();

			light.type = LightType.Directional;
			light.shadows = LightShadows.Soft;

			return util;
		}

		private void Awake()
		{
			SetDefaultOrientation();
		}

		private void SetDefaultOrientation() => transform.rotation = Quaternion.Euler(75f, 60f, 0f);

		public void UpdateFromSettings(Color value, float[] skyvec = null, float intensity = 1f)
		{
			directionalLight.color = value;
			directionalLight.intensity = intensity;
			if (null != skyvec && skyvec.Length >= 2)
				transform.rotation = Quaternion.LookRotation(-LinearCubemapUtility.UVToDirection(new Vector2(skyvec[0], skyvec[1])));
			else
				SetDefaultOrientation();
			ClearCache();
		}

		public void UpdateColour(Color value, float intensity = 1f)
		{
			directionalLight.color = value;
			directionalLight.intensity = intensity;
		}

		public void UpdateDirection(Vector3 direction) => transform.rotation = Quaternion.LookRotation(direction);

		/// <summary>
		/// Updates the directional light using a tinted cubemap.
		/// Bright color uses CubemapUtility's cache.
		/// Light direction uses a simple local cache (only direction finding is expensive).
		/// </summary>
		public Color UpdateFromTintendCubemap(Cubemap cubemap = null)
		{
			if (cubemap == null)
			{
				directionalLight.color = Color.white;
				directionalLight.intensity = intensityMultiplier;
				ClearCache();
				return directionalLight.color;
			}

			// Bright color – fully handled by CubemapUtility cache (no local duplicate)
			var brightColor = CubemapUtility.ComputeBrightColor(cubemap, threshold);

			// Light Direction – local cache (only recompute when cubemap actually changes)
			Vector3 lightDir;
			if (cubemap == _lastCubemapForDirection && _cachedLightDirection.sqrMagnitude > 0.001f)
			{
				lightDir = _cachedLightDirection;
			}
			else
			{
				//var lightDir = AtlasCubemapUtility.FindLightDirection(cubemap);
				//var lightDir = EquirectangularCubemapUtility.FindLightDirection(cubemap);
				lightDir = LinearCubemapUtility.FindLightDirection(cubemap);

				_cachedLightDirection = lightDir;
				_lastCubemapForDirection = cubemap;
			}

			// Apply to light
			directionalLight.color = brightColor;

			var lum = brightColor.Luminance();
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

			//Debug.Log($"Sky updated: BrightColor={brightColor}, Lum={lum:F3}, Intensity={directionalLight.intensity:F2}");

			return directionalLight.color;
		}

		public void CopyFrom(DirectionalLightUtility other)
		{
			var otherLight = other.GetComponent<Light>();

			directionalLight.color = otherLight.color;
			directionalLight.intensity = otherLight.intensity;
			transform.rotation = other.transform.rotation;

			ClearCache();
		}

		/// <summary>
		/// Clears the light direction cache.
		/// </summary>
		private void ClearCache()
		{
			_lastCubemapForDirection = null;
			_cachedLightDirection = Vector3.zero;
		}
	}
}

//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	[RequireComponent(typeof(Light))]
//	public class DirectionalLightUtility : MonoBehaviour
//	{
//		[Header("Lighting Settings")]
//		[Range(0.005f, 1.0f)]
//		[SerializeField] private float threshold = 0.85f;

//		[Range(1f, 20f)]
//		[SerializeField] private float intensityMultiplier = 1.2f;

//		[Range(0f, 3f)]
//		[SerializeField] private float minIntensity = 0f;

//		private Light directionalLight => GetComponent<Light>();

//		public static DirectionalLightUtility Instantiate(Transform parent = null)
//		{
//			var instance = new GameObject("DirectionalLight", typeof(DirectionalLightUtility));
//			if (parent != null)
//				instance.transform.parent = parent;

//			var util = instance.GetComponent<DirectionalLightUtility>();
//			var light = util.GetComponent<Light>();

//			light.type = LightType.Directional;
//			light.shadows = LightShadows.Soft;

//			return util;
//		}

//		private void Awake()
//		{
//			SetDefaultOrientation();
//			//SkyboxUtility.OnSkyboxChanged += UpdateFromSkybox;
//		}

//		private void OnDestroy()
//		{
//			//SkyboxUtility.OnSkyboxChanged -= UpdateFromSkybox;
//		}

//		private void SetDefaultOrientation() => transform.rotation = Quaternion.Euler(75f, 60f, 0f);//default

//		public void UpdateFromSettings(Color value, float intensity = 1f)
//		{
//			directionalLight.color = value;
//			directionalLight.intensity = intensity;
//			SetDefaultOrientation();
//		}

//		//public Color UpdateFromSkyboxMaterial(Material skybox = null) => UpdateFromTintendCubemap(CubemapUtility.GetTintedCubemap(skybox));

//		public Color UpdateFromTintendCubemap(Cubemap cubemap = null)
//		{
//			if (cubemap == null)
//			{
//				directionalLight.color = Color.white;
//				directionalLight.intensity = intensityMultiplier;
//				return directionalLight.color;
//			}

//			// Bright color using your existing utility
//			var brightColor = CubemapUtility.ComputeBrightColor(cubemap, threshold);

//			// Light direction
//			//var lightDir = AtlasCubemapUtility.FindLightDirection(cubemap);
//			//var lightDir = EquirectangularCubemapUtility.FindLightDirection(cubemap);
//			var lightDir = LinearCubemapUtility.FindLightDirection(cubemap);

//			// Apply to light
//			directionalLight.color = brightColor;

//			var lum = brightColor.Luminance();
//			directionalLight.intensity = Mathf.Max(minIntensity, lum * intensityMultiplier);

//			// Apply direction
//			if (lightDir.sqrMagnitude > 0.001f)
//			{
//				transform.rotation = Quaternion.LookRotation(lightDir.normalized);
//			}
//			else
//			{
//				transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, -1f, 0.5f).normalized);
//				Debug.LogWarning("No light direction determined - resorting to default");
//			}

//			Debug.Log($"Sky updated: BrightColor={brightColor}, Lum={lum:F3}, Intensity={directionalLight.intensity:F2}");

//			return directionalLight.color;
//		}

//		public void CopyFrom(DirectionalLightUtility other)
//		{
//			var otherLight = other.GetComponent<Light>();

//			directionalLight.color = otherLight.color;
//			directionalLight.intensity = otherLight.intensity;
//			transform.rotation = other.transform.rotation;
//		}
//	}
//}