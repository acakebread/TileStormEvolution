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
			[Range(0.1f, 10f)] public float cycleTime = 0.1f;
			[SerializeField] public List<Pulse> pulses = new List<Pulse> { new Pulse { start = 0f, end = 0.1f } };
			[Range(1, 128)] public int particleCount = 1;
			public Vector3 velocity = Vector3.zero;
			[Range(0f, 10f)] public float scatter = 0f;

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
		}

		private List<ParticleData> activeParticles;
		private bool emitEnabled;
		private float timelinePosition;
		private float lastTimelinePosition;

		void Awake()
		{
			activeParticles = new List<ParticleData>();

			if (particleMaterial == null)
			{
				Debug.LogError("ParticleController: particleMaterial is not assigned!");
				enabled = false;
				return;
			}
			if (settings == null)
			{
				Debug.LogError("ParticleController: settings is not assigned!");
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
			timelinePosition = lastTimelinePosition = 0f;
			emitEnabled = false;
		}

		void FixedUpdate()
		{
			if (settings.updateParticles)
				UpdateParticles();

			// PWM timeline
			lastTimelinePosition = timelinePosition;
			timelinePosition += Time.deltaTime;
			if (timelinePosition >= settings.cycleTime)
				timelinePosition -= settings.cycleTime;

			float normalizedTime = timelinePosition / settings.cycleTime;
			float lastNormalizedTime = lastTimelinePosition / settings.cycleTime;
			bool inPulse = false;

			foreach (var pulse in settings.pulses)
			{
				if (normalizedTime >= pulse.start && normalizedTime <= pulse.end)
					inPulse = true;

				bool crossed = false;
				if (lastNormalizedTime <= normalizedTime)
				{
					if (lastNormalizedTime < pulse.end && normalizedTime > pulse.start)
						crossed = true;
				}
				else
				{
					if (lastNormalizedTime < pulse.end || normalizedTime > pulse.start)
						crossed = true;
				}

				if (crossed)
					EmitParticlesInternal();
			}

			if (emitEnabled || inPulse)
				EmitParticlesInternal();
		}

		void Update()
		{
			customParticleSystem.Render();
		}

		public void EmitParticles() { emitEnabled = true; timelinePosition = 0f; }
		public void StopEmitting() { emitEnabled = false; timelinePosition = 0f; }

		private void EmitParticlesInternal()
		{
			int emitCount = Mathf.Max(1, settings.particleCount);
			for (int n = 0; n < emitCount; ++n)
			{
				var scatter = Random.value * settings.scatter * Random.onUnitSphere;
				SpawnParticle(transform.position, settings.velocity + scatter, settings.lifetimeVariation);
			}
		}

		public void SpawnParticle(Vector3 position, Vector3 velocity,
								  float? lifetimeVariation = null,
								  ParticleSettings customSettings = null)
		{
			if (!settings.updateParticles) return;

			var s = customSettings ?? settings;
			float variation = lifetimeVariation ?? s.lifetimeVariation;
			float lifetime = s.lifetime + Random.Range(-variation, variation);

			float initialScale = s.scaleCurve.Evaluate(0f);
			float initialRadius = s.radius * initialScale;

			// ----- NEW CALL: 4 arguments -----
			int poolIndex = customParticleSystem.SpawnParticle(position, lifetime, initialRadius, s.color);
			if (poolIndex == -1) return;

			var pd = new ParticleData
			{
				poolIndex = poolIndex,
				position = position,
				velocity = velocity,
				lifetime = lifetime,
				maxLifetime = lifetime,
				color = s.color,
				radius = initialRadius,
				initialRadius = s.radius
			};

			activeParticles.Add(pd);
		}

		private void UpdateParticles()
		{
			float dt = Time.deltaTime;

			for (int i = activeParticles.Count - 1; i >= 0; i--)
			{
				var pd = activeParticles[i];

				if (pd.lifetime <= 0f)
				{
					activeParticles.RemoveAt(i);
					continue;
				}

				pd.lifetime -= dt;

				if (pd.lifetime <= 0f)
				{
					customParticleSystem.UpdateParticle(pd.poolIndex, pd.position, 0f, pd.radius, pd.color);
					activeParticles.RemoveAt(i);
					continue;
				}

				// fade
				float norm = 1f - Mathf.Clamp01(pd.lifetime / pd.maxLifetime);
				float alpha = (norm < settings.fadeStartTime || Mathf.Approximately(settings.fadeStartTime, 1f))
							  ? 1f
							  : Mathf.Clamp01(1f - ((norm - settings.fadeStartTime) / (1f - settings.fadeStartTime)));
				pd.color.a = alpha;

				// scale
				float scale = settings.scaleCurve.Evaluate(norm);
				pd.radius = pd.initialRadius * scale;

				// physics
				pd.velocity.y -= settings.gravity * dt;
				pd.position += pd.velocity * dt;

				float groundY = settings.useGlobalGroundPlane
								? settings.groundHeight
								: transform.position.y + settings.groundHeight;

				if (pd.velocity.y < 0f && pd.position.y <= groundY)
				{
					pd.position.y = groundY;
					pd.velocity.y = -pd.velocity.y;
					pd.velocity *= settings.bounceDamping;
				}

				customParticleSystem.UpdateParticle(pd.poolIndex, pd.position, pd.lifetime, pd.radius, pd.color);
			}
		}
	}
}