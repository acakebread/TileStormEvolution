using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	public class ParticleController : MonoBehaviour
	{
		[System.Serializable]
		public class ParticleSettings
		{
			//public float speed = 4f;
			public float lifetime = 1f;
			public float lifetimeVariation = 0.5f;
			public float radius = 0.02f;
			public AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
			public Color color = Color.white;
			public float gravity = 10f;
			public float bounceDamping = 0.8f;
			public float groundHeight = 0f;
			public bool useGlobalGroundPlane = true;
			public bool useThreeZoneSlicing = false;
			public bool updateParticles = true;
			[Range(0f, 1f)] public float fadeStartTime = 1f;
			[Range(0.1f, 10f)] public float cycleTime = 0.1f; // Total PWM cycle duration (seconds)
			[SerializeField] public List<Pulse> pulses = new List<Pulse> { new Pulse { start = 0f, end = 0.1f } }; // List of pulses
			[Range(1, 128)] public int particleCount = 1;
			public Vector3 velocity = Vector3.zero;

			[System.Serializable]
			public class Pulse
			{
				[Range(0f, 1f)] public float start;
				[Range(0f, 1f)] public float end;
			}
		}

		[SerializeField] private Material particleMaterial;
		[SerializeField] public ParticleSettings settings;

		private ParticleSystem customParticleSystem;
		private class ParticleData
		{
			public int poolIndex;
			public Vector3 position;
			public Vector3 velocity;
			public float lifetime;
			public float maxLifetime;
			public Color color;
			public float radius;
			public float initialRadius;
			public float tipSize;
			public bool isActive;
		}

		private List<ParticleData> activeParticles;
		private bool emitEnabled; // Forces continuous emission when true
		private float timelinePosition; // Tracks position in the PWM timeline (seconds)
		private float lastTimelinePosition; // Tracks previous frame's position

		void Awake()
		{
			activeParticles = new List<ParticleData>();
			if (particleMaterial == null)
			{
				Debug.LogError("ParticleController: particleMaterial is not assigned! Please assign a material with the 'MassiveHadronLtd/Unlit/AdditiveParticles' shader and a particle texture.");
				enabled = false;
				return;
			}
			if (settings == null)
			{
				Debug.LogError("ParticleController: settings is not assigned! Please assign ParticleSettings in the Inspector.");
				enabled = false;
				return;
			}

			if (settings.scaleCurve.keys.Length == 0)
			{
				settings.scaleCurve = new AnimationCurve();
				settings.scaleCurve.AddKey(new Keyframe(0f, 1.0f, 0f, 0f));
				settings.scaleCurve.AddKey(new Keyframe(1f, 1.0f, 0f, 0f));
#if UNITY_EDITOR
				for (int i = 0; i < settings.scaleCurve.keys.Length; i++)
				{
					AnimationUtility.SetKeyBroken(settings.scaleCurve, i, true);
					AnimationUtility.SetKeyLeftTangentMode(settings.scaleCurve, i, AnimationUtility.TangentMode.Free);
					AnimationUtility.SetKeyRightTangentMode(settings.scaleCurve, i, AnimationUtility.TangentMode.Free);
				}
#endif
			}

			customParticleSystem = new ParticleSystem(particleMaterial, settings.useThreeZoneSlicing);
			timelinePosition = 0f;
			lastTimelinePosition = 0f;
			emitEnabled = false;
		}

		void FixedUpdate()
		{
			if (settings.updateParticles)
				UpdateParticles();

			// Advance PWM timeline
			lastTimelinePosition = timelinePosition;
			timelinePosition += Time.deltaTime;
			if (timelinePosition >= settings.cycleTime)
				timelinePosition -= settings.cycleTime;

			// Check for emission
			float normalizedTime = timelinePosition / settings.cycleTime;
			float lastNormalizedTime = lastTimelinePosition / settings.cycleTime;
			bool inPulse = false;

			foreach (var pulse in settings.pulses)
			{
				// Check if currently in pulse
				if (normalizedTime >= pulse.start && normalizedTime <= pulse.end)
				{
					inPulse = true;
				}
				// Check if pulse was crossed (including wrap-around)
				else
				{
					bool crossedPulse = false;
					if (lastNormalizedTime <= normalizedTime)
					{
						// Normal progression
						if (lastNormalizedTime < pulse.end && normalizedTime > pulse.start)
							crossedPulse = true;
					}
					else
					{
						// Wrap-around (cycle reset)
						if (lastNormalizedTime < pulse.end || normalizedTime > pulse.start)
							crossedPulse = true;
					}

					if (crossedPulse)
						EmitParticlesInternal(); // Emit once for crossed pulse
				}
			}

			if (emitEnabled || inPulse)
				EmitParticlesInternal();
		}

		void Update()
		{
			customParticleSystem.Render();
		}

		public void EmitParticles()
		{
			emitEnabled = true;
			timelinePosition = 0f; // Reset timeline on start
		}

		public void StopEmitting()
		{
			emitEnabled = false;
			timelinePosition = 0f; // Reset timeline on stop
		}

		private void EmitParticlesInternal()
		{
			// Emit exactly particleCount particles per frame
			int emitCount = Mathf.Max(1, settings.particleCount);
			for (int n = 0; n < emitCount; ++n)
			{
				//var vel = Random.onUnitSphere * Random.value * 0.5f;
				//vel.y *= 2f;
				//vel += settings.velocityBias;
				//Vector3 worldPos = transform.position;
				//SpawnParticle(worldPos, vel, settings.lifetimeVariation);
				//settings.velocity = Vector3.forward;
				SpawnParticle(transform.position, settings.velocity, settings.lifetimeVariation);
			}
		}

		public void SpawnParticle(Vector3 position, Vector3 velocity, float? lifetimeVariation = null, ParticleSettings customSettings = null)
		{
			if (!settings.updateParticles) return;
			var activeSettings = customSettings ?? settings;
			float variation = lifetimeVariation ?? activeSettings.lifetimeVariation;

			float lifetime = activeSettings.lifetime + Random.Range(-variation, variation);
			//lifetime = Mathf.Max(0.1f, lifetime);

			float initialScale = activeSettings.scaleCurve.Evaluate(0f);
			float initialRadius = activeSettings.radius * initialScale;

			var particleSettings = new ParticleSystem.ParticleSettings
			{
				lifetime = lifetime,
				radius = initialRadius,
				decay = false,
				color = activeSettings.color
			};

			//int poolIndex = customParticleSystem.SpawnParticle(position, velocity * activeSettings.speed, particleSettings);
			int poolIndex = customParticleSystem.SpawnParticle(position, particleSettings);
			if (poolIndex == -1) return;

			ParticleData particle = new ParticleData
			{
				poolIndex = poolIndex,
				position = position,
				velocity = velocity,// * activeSettings.speed,
				lifetime = lifetime,
				maxLifetime = lifetime,
				color = activeSettings.color,
				radius = initialRadius,
				initialRadius = activeSettings.radius,
				tipSize = initialRadius * 0.5f,
				isActive = true
			};

			activeParticles.Add(particle);
		}

		private void UpdateParticles()
		{
			float deltaTime = Time.deltaTime;

			for (int i = activeParticles.Count - 1; i >= 0; i--)
			{
				ParticleData particle = activeParticles[i];
				if (!particle.isActive)
				{
					activeParticles.RemoveAt(i);
					continue;
				}

				particle.lifetime -= deltaTime;

				if (particle.lifetime <= 0f)
				{
					particle.isActive = false;
					activeParticles.RemoveAt(i);
					customParticleSystem.UpdateParticle(particle.poolIndex, particle.position, 0f, particle.radius, particle.tipSize, particle.color);
					continue;
				}

				float normalizedTime = 1f - Mathf.Clamp01(particle.lifetime / particle.maxLifetime);
				float alpha;
				if (normalizedTime < settings.fadeStartTime || Mathf.Approximately(settings.fadeStartTime, 1f))
				{
					alpha = 1f;
				}
				else
				{
					float fadeDuration = 1f - settings.fadeStartTime;
					alpha = fadeDuration > 0.0001f ? 1f - ((normalizedTime - settings.fadeStartTime) / fadeDuration) : 0f;
					alpha = Mathf.Clamp01(alpha);
				}
				particle.color.a = alpha;

				float scaleFactor = settings.scaleCurve.Evaluate(normalizedTime);
				particle.radius = particle.initialRadius * scaleFactor;
				particle.tipSize = particle.radius * 0.5f;

				particle.velocity.y -= settings.gravity * deltaTime;
				particle.position += particle.velocity * deltaTime;

				float currentY = particle.position.y;
				float groundY = settings.groundHeight;
				if (!settings.useGlobalGroundPlane)
				{
					groundY = transform.position.y + settings.groundHeight;
				}

				if (particle.velocity.y < 0 && currentY <= groundY)
				{
					particle.position.y = groundY;
					particle.velocity.y = -particle.velocity.y * settings.bounceDamping;
				}

				customParticleSystem.UpdateParticle(particle.poolIndex, particle.position, particle.lifetime, particle.radius, particle.tipSize, particle.color);
			}
		}
	}
}