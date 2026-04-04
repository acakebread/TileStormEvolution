using UnityEngine;
using UnityEngine.Rendering;

namespace MassiveHadronLtd
{
	[System.Serializable]
	public readonly struct UnityRenderSettings
	{
		// Core fields you already had
		public readonly AmbientMode ambientMode;
		public readonly Color ambientLight;
		public readonly float ambientIntensity;
		public readonly Material skybox;
		public readonly SphericalHarmonicsL2 ambientProbe;
		public readonly Color subtractiveShadowColor;

		// Additional common RenderSettings fields (easy to expand later)
		public readonly Color ambientSkyColor;
		public readonly Color ambientEquatorColor;
		public readonly Color ambientGroundColor;
		public readonly bool fog;
		public readonly FogMode fogMode;
		public readonly Color fogColor;
		public readonly float fogDensity;
		public readonly float fogStartDistance;
		public readonly float fogEndDistance;

		// Keep the original constructor signature exactly as-is
		public UnityRenderSettings(
			AmbientMode ambientMode,
			Color ambientLight,
			float ambientIntensity,
			Material skybox,
			SphericalHarmonicsL2 ambientProbe,
			Color subtractiveShadowColor)
			: this(ambientMode, ambientLight, ambientIntensity, skybox, ambientProbe, subtractiveShadowColor,
				   RenderSettings.ambientSkyColor,
				   RenderSettings.ambientEquatorColor,
				   RenderSettings.ambientGroundColor,
				   RenderSettings.fog,
				   RenderSettings.fogMode,
				   RenderSettings.fogColor,
				   RenderSettings.fogDensity,
				   RenderSettings.fogStartDistance,
				   RenderSettings.fogEndDistance)
		{
		}

		// Full constructor (used internally by extensions and CaptureCurrent)
		public UnityRenderSettings(
			AmbientMode ambientMode,
			Color ambientLight,
			float ambientIntensity,
			Material skybox,
			SphericalHarmonicsL2 ambientProbe,
			Color subtractiveShadowColor,
			Color ambientSkyColor,
			Color ambientEquatorColor,
			Color ambientGroundColor,
			bool fog,
			FogMode fogMode,
			Color fogColor,
			float fogDensity,
			float fogStartDistance,
			float fogEndDistance)
		{
			this.ambientMode = ambientMode;
			this.ambientLight = ambientLight;
			this.ambientIntensity = ambientIntensity;
			this.skybox = skybox;
			this.ambientProbe = ambientProbe;
			this.subtractiveShadowColor = subtractiveShadowColor;

			this.ambientSkyColor = ambientSkyColor;
			this.ambientEquatorColor = ambientEquatorColor;
			this.ambientGroundColor = ambientGroundColor;
			this.fog = fog;
			this.fogMode = fogMode;
			this.fogColor = fogColor;
			this.fogDensity = fogDensity;
			this.fogStartDistance = fogStartDistance;
			this.fogEndDistance = fogEndDistance;
		}

		public static UnityRenderSettings CaptureCurrent() =>
			new(
				RenderSettings.ambientMode,
				RenderSettings.ambientLight,
				RenderSettings.ambientIntensity,
				RenderSettings.skybox,
				RenderSettings.ambientProbe,
				RenderSettings.subtractiveShadowColor,
				RenderSettings.ambientSkyColor,
				RenderSettings.ambientEquatorColor,
				RenderSettings.ambientGroundColor,
				RenderSettings.fog,
				RenderSettings.fogMode,
				RenderSettings.fogColor,
				RenderSettings.fogDensity,
				RenderSettings.fogStartDistance,
				RenderSettings.fogEndDistance
			);

		public void ApplyToRenderSettings()
		{
			RenderSettings.ambientMode = ambientMode;
			RenderSettings.ambientLight = ambientLight;
			RenderSettings.ambientIntensity = ambientIntensity;
			RenderSettings.skybox = skybox;
			RenderSettings.ambientProbe = ambientProbe;
			RenderSettings.subtractiveShadowColor = subtractiveShadowColor;

			RenderSettings.ambientSkyColor = ambientSkyColor;
			RenderSettings.ambientEquatorColor = ambientEquatorColor;
			RenderSettings.ambientGroundColor = ambientGroundColor;
			RenderSettings.fog = fog;
			RenderSettings.fogMode = fogMode;
			RenderSettings.fogColor = fogColor;
			RenderSettings.fogDensity = fogDensity;
			RenderSettings.fogStartDistance = fogStartDistance;
			RenderSettings.fogEndDistance = fogEndDistance;
		}

		public static void Restore(in UnityRenderSettings settings) => settings.ApplyToRenderSettings();
		public static void Apply(in UnityRenderSettings settings) => settings.ApplyToRenderSettings();
	}

	// ─────────────────────────────────────────────────────────────
	// Extension methods — this is what gives you the nice API
	// ─────────────────────────────────────────────────────────────
	public static class UnityRenderSettingsExtensions
	{
        public static UnityRenderSettings ReplaceSkybox(this UnityRenderSettings s, Material skybox) =>
            new(s.ambientMode, s.ambientLight, s.ambientIntensity, skybox,
                s.ambientProbe, s.subtractiveShadowColor);

        public static UnityRenderSettings ReplaceAmbientLight(this UnityRenderSettings s, Color ambientLight) =>
            new(s.ambientMode, ambientLight, s.ambientIntensity, s.skybox,
                s.ambientProbe, s.subtractiveShadowColor);

        public static UnityRenderSettings ReplaceAmbientIntensity(this UnityRenderSettings s, float ambientIntensity) =>
            new(s.ambientMode, s.ambientLight, ambientIntensity, s.skybox,
                s.ambientProbe, s.subtractiveShadowColor);

        public static UnityRenderSettings ReplaceAmbientMode(this UnityRenderSettings s, AmbientMode mode) =>
            new(mode, s.ambientLight, s.ambientIntensity, s.skybox,
                s.ambientProbe, s.subtractiveShadowColor);

        public static UnityRenderSettings ReplaceAmbientProbe(this UnityRenderSettings s, SphericalHarmonicsL2 probe) =>
            new(s.ambientMode, s.ambientLight, s.ambientIntensity, s.skybox,
                probe, s.subtractiveShadowColor);

        public static UnityRenderSettings ReplaceSubtractiveShadowColor(this UnityRenderSettings s, Color color) =>
            new(s.ambientMode, s.ambientLight, s.ambientIntensity, s.skybox,
                s.ambientProbe, color);
	}
}

//using UnityEngine;
//using UnityEngine.Rendering;

//namespace MassiveHadronLtd
//{
//	[System.Serializable]
//	public readonly struct UnityRenderSettings
//	{
//		public readonly AmbientMode ambientMode;
//		public readonly Color ambientLight;
//		public readonly float ambientIntensity;
//		public readonly Material skybox;
//		public readonly SphericalHarmonicsL2 ambientProbe;
//		public readonly Color subtractiveShadowColor;

//		// Add more fields here later if your preview code starts touching them
//		// (fog, reflectionIntensity, customReflection, etc.)

//		public UnityRenderSettings(
//			AmbientMode ambientMode,
//			Color ambientLight,
//			float ambientIntensity,
//			Material skybox,
//			SphericalHarmonicsL2 ambientProbe,
//			Color subtractiveShadowColor)
//		{
//			this.ambientMode = ambientMode;
//			this.ambientLight = ambientLight;
//			this.ambientIntensity = ambientIntensity;
//			this.skybox = skybox;
//			this.ambientProbe = ambientProbe;
//			this.subtractiveShadowColor = subtractiveShadowColor;
//		}

//		public static UnityRenderSettings CaptureCurrent()
//		{
//			return new UnityRenderSettings(
//				RenderSettings.ambientMode,
//				RenderSettings.ambientLight,
//				RenderSettings.ambientIntensity,
//				RenderSettings.skybox,
//				RenderSettings.ambientProbe,
//				RenderSettings.subtractiveShadowColor
//			);
//		}

//		public void ApplyToRenderSettings()
//		{
//			RenderSettings.ambientMode = ambientMode;
//			RenderSettings.ambientLight = ambientLight;
//			RenderSettings.ambientIntensity = ambientIntensity;
//			RenderSettings.skybox = skybox;
//			RenderSettings.ambientProbe = ambientProbe;
//			RenderSettings.subtractiveShadowColor = subtractiveShadowColor;
//		}

//		public static UnityRenderSettings Clone() => CaptureCurrent();

//		public static void Restore(in UnityRenderSettings settings) => settings.ApplyToRenderSettings();
//		public static void Apply(in UnityRenderSettings settings) => settings.ApplyToRenderSettings();
//	}

//	public static class RenderSettingsExtensions
//	{
//		public static UnityRenderSettings Clone(this RenderSettings _)
//		{
//			return UnityRenderSettings.CaptureCurrent();
//		}

//		public static void Restore(this RenderSettings _, UnityRenderSettings settings)
//		{
//			settings.ApplyToRenderSettings();
//		}

//		// Optional: if you prefer "Apply" naming over "Restore"
//		public static void Apply(this RenderSettings _, UnityRenderSettings settings)
//		{
//			settings.ApplyToRenderSettings();
//		}
//	}
//}