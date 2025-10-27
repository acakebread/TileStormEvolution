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
			public float speed = 4f;
			public float lifetime = 1f;
			public float lifetimeVariation = 0.5f;
			public float width = 0.02f;
			public AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
			public Color color = Color.white;
			public float gravity = 10f;
			public float bounceDamping = 0.8f;
			public float groundHeight = 0f;
			public bool useGlobalGroundPlane = true;
			public bool useThreeZoneSlicing = false;
			public bool updateParticles = true;
			[Range(0f, 1f)] public float fadeStartTime = 1f;
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
			public float width;
			public float initialWidth;
			public float tipSize;
			public bool isActive;
		}

		private List<ParticleData> activeParticles;

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
		}

		void FixedUpdate()
		{
			if (settings.updateParticles)
				UpdateParticles();
		}

		void Update()
		{
			customParticleSystem.Render();
		}

		public void SpawnParticle(Vector3 position, Vector3 velocity, float? lifetimeVariation = null, ParticleSettings customSettings = null)
		{
			if (!settings.updateParticles) return;
			var activeSettings = customSettings ?? settings;
			float variation = lifetimeVariation ?? activeSettings.lifetimeVariation;

			float lifetime = activeSettings.lifetime + Random.Range(-variation, variation);
			lifetime = Mathf.Max(0.1f, lifetime);

			float initialScale = activeSettings.scaleCurve.Evaluate(0f);
			float initialWidth = activeSettings.width * initialScale;

			var particleSettings = new ParticleSystem.ParticleSettings
			{
				lifetime = lifetime,
				width = initialWidth,
				decay = false,
				color = activeSettings.color
			};

			int poolIndex = customParticleSystem.SpawnParticle(position, velocity * activeSettings.speed, particleSettings);
			if (poolIndex == -1) return;

			ParticleData particle = new ParticleData
			{
				poolIndex = poolIndex,
				position = position,
				velocity = velocity * activeSettings.speed,
				lifetime = lifetime,
				maxLifetime = lifetime,
				color = activeSettings.color,
				width = initialWidth,
				initialWidth = activeSettings.width,
				tipSize = initialWidth * 0.5f,
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
					customParticleSystem.UpdateParticle(particle.poolIndex, particle.position, particle.velocity, 0f, particle.width, particle.tipSize, particle.color);
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
				particle.width = particle.initialWidth * scaleFactor;
				particle.tipSize = particle.width * 0.54f;

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

				customParticleSystem.UpdateParticle(particle.poolIndex, particle.position, particle.velocity, particle.lifetime, particle.width, particle.tipSize, particle.color);
			}
		}
	}
}