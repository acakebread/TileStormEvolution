using UnityEngine;
using UnityEngine.Rendering;

namespace MassiveHadronLtd
{
	[System.Serializable]
	public readonly struct UnityRenderSettings
	{
		public readonly AmbientMode ambientMode;
		public readonly Color ambientLight;
		public readonly float ambientIntensity;
		public readonly Material skybox;
		public readonly SphericalHarmonicsL2 ambientProbe;
		public readonly Color subtractiveShadowColor;

		// Add more fields here later if your preview code starts touching them
		// (fog, reflectionIntensity, customReflection, etc.)

		public UnityRenderSettings(
			AmbientMode ambientMode,
			Color ambientLight,
			float ambientIntensity,
			Material skybox,
			SphericalHarmonicsL2 ambientProbe,
			Color subtractiveShadowColor)
		{
			this.ambientMode = ambientMode;
			this.ambientLight = ambientLight;
			this.ambientIntensity = ambientIntensity;
			this.skybox = skybox;
			this.ambientProbe = ambientProbe;
			this.subtractiveShadowColor = subtractiveShadowColor;
		}

		public static UnityRenderSettings CaptureCurrent()
		{
			return new UnityRenderSettings(
				RenderSettings.ambientMode,
				RenderSettings.ambientLight,
				RenderSettings.ambientIntensity,
				RenderSettings.skybox,
				RenderSettings.ambientProbe,
				RenderSettings.subtractiveShadowColor
			);
		}

		public void ApplyToRenderSettings()
		{
			RenderSettings.ambientMode = ambientMode;
			RenderSettings.ambientLight = ambientLight;
			RenderSettings.ambientIntensity = ambientIntensity;
			RenderSettings.skybox = skybox;
			RenderSettings.ambientProbe = ambientProbe;
			RenderSettings.subtractiveShadowColor = subtractiveShadowColor;
		}

		public static UnityRenderSettings Clone() => CaptureCurrent();

		public static void Restore(in UnityRenderSettings settings) => settings.ApplyToRenderSettings();
		public static void Apply(in UnityRenderSettings settings) => settings.ApplyToRenderSettings();
	}

	public static class RenderSettingsExtensions
	{
		public static UnityRenderSettings Clone(this RenderSettings _)
		{
			return UnityRenderSettings.CaptureCurrent();
		}

		public static void Restore(this RenderSettings _, UnityRenderSettings settings)
		{
			settings.ApplyToRenderSettings();
		}

		// Optional: if you prefer "Apply" naming over "Restore"
		public static void Apply(this RenderSettings _, UnityRenderSettings settings)
		{
			settings.ApplyToRenderSettings();
		}
	}
}