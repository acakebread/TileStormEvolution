using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	public class ParticleController : MonoBehaviour
	{
		#region --- Exposed Settings (formerly ParticleSettings) ---
		[Header("Lifetime")]
		public float lifetime = 1f;
		public float lifetimeVariation = 0.5f;

		[Header("Appearance")]
		public float radius = 0.02f;
		public AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
		public Color color = Color.white;

		[Header("Physics")]
		public float gravity = 10f;
		public float bounceDamping = 0.8f;
		public float groundHeight = 0f;
		public bool useGlobalGroundPlane = true;

		[Header("Rendering")]
		public bool useThreeZoneSlicing = false;
		public bool updateParticles = true;

		[Header("Fade")]
		[Range(0f, 1f)] public float fadeStartTime = 1f;

		[Header("PWM / Emission")]
		[Range(0.1f, 10f)] public float cycleTime = 0.1f;
		[SerializeField] public List<Pulse> pulses = new List<Pulse> { new Pulse { start = 0f, end = 0.1f } };
		[Range(1, 128)] public int particleCount = 1;
		public Vector3 velocity = Vector3.zero;
		[Range(0f, 10f)] public float scatter = 0f;
		#endregion

		[System.Serializable]
		public class Pulse
		{
			[Range(0f, 1f)] public float start;
			[Range(0f, 1f)] public float end;
		}

		[SerializeField] private Material particleMaterial;

		private ParticleSystem customParticleSystem;

		private class ParticleData : ParticleSystem.ParticleDataRoot
		{
			public ParticleSystem.Particle particle;   // Direct reference
			public Vector3 position;
			public Vector3 velocity;
			public float maxLifetime;
			public Color color;
			public float initialRadius;
		}

		private bool emitEnabled;
		private float timelinePosition;
		private float lastTimelinePosition;

		void Awake()
		{
			if (particleMaterial == null)
			{
				Debug.LogError("ParticleController: particleMaterial is not assigned!");
				enabled = false;
				return;
			}

			// Initialise default scale curve if empty
			if (scaleCurve.keys.Length == 0)
			{
				scaleCurve = new AnimationCurve();
				scaleCurve.AddKey(new Keyframe(0f, 1.0f, 0f, 0f));
				scaleCurve.AddKey(new Keyframe(1f, 1.0f, 0f, 0f));
#if UNITY_EDITOR
				for (int i = 0; i < scaleCurve.keys.Length; i++)
				{
					AnimationUtility.SetKeyBroken(scaleCurve, i, true);
					AnimationUtility.SetKeyLeftTangentMode(scaleCurve, i, AnimationUtility.TangentMode.Free);
					AnimationUtility.SetKeyRightTangentMode(scaleCurve, i, AnimationUtility.TangentMode.Free);
				}
#endif
			}

			customParticleSystem = new ParticleSystem(particleMaterial, useThreeZoneSlicing);
			timelinePosition = lastTimelinePosition = 0f;
			emitEnabled = false;
		}

		void FixedUpdate()
		{
			if (updateParticles)
				UpdateParticles();

			lastTimelinePosition = timelinePosition;
			timelinePosition += Time.deltaTime;
			if (timelinePosition >= cycleTime)
				timelinePosition -= cycleTime;

			float normalizedTime = timelinePosition / cycleTime;
			float lastNormalizedTime = lastTimelinePosition / cycleTime;
			bool inPulse = false;

			foreach (var pulse in pulses)
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
			int emitCount = Mathf.Max(1, particleCount);
			for (int n = 0; n < emitCount; ++n)
			{
				var scatterVec = Random.value * scatter * Random.onUnitSphere;
				SpawnParticle(transform.position, velocity + scatterVec, lifetimeVariation);
			}
		}

		public void SpawnParticle(Vector3 position, Vector3 velocity, float? lifetimeVariation = null)
		{
			if (!updateParticles) return;

			var variation = lifetimeVariation ?? this.lifetimeVariation;
			var life = lifetime + variation + Random.Range(-variation, variation);
			if (life <= 0f)
			{
				Debug.LogError($"spawning particle with no life span {life}");
				return;
			}

			var particle = customParticleSystem.AllocateParticle();
			if (particle == null) return;

			particle.delta = Vector3.zero;
			particle.position = position;
			particle.life = life;
			particle.color = color;

			particle.particleData = new ParticleData
			{
				particle = particle,
				position = position,
				velocity = velocity,
				maxLifetime = life,
				color = color,
				initialRadius = radius * scaleCurve.Evaluate(0f)
			};
		}

		private void UpdateParticles()
		{
			float dt = Time.deltaTime;

			for (int i = customParticleSystem.activeParticles.Count - 1; i >= 0; i--)
			{
				var pd = customParticleSystem.activeParticles[i].particleData as ParticleData;

				// ----- Fade -----
				float norm = 1f - Mathf.Clamp01(pd.particle.life / pd.maxLifetime);
				float alpha = (norm < fadeStartTime || Mathf.Approximately(fadeStartTime, 1f))
							   ? 1f
							   : Mathf.Clamp01(1f - ((norm - fadeStartTime) / (1f - fadeStartTime)));
				pd.color.a = alpha;

				// ----- Scale -----
				float scale = scaleCurve.Evaluate(norm);
				var radius = pd.initialRadius * scale;

				// ----- Physics -----
				pd.velocity.y -= gravity * dt;
				pd.position += pd.velocity * dt;

				float groundY = useGlobalGroundPlane ? groundHeight : transform.position.y + groundHeight;

				if (pd.velocity.y < 0f && pd.position.y <= groundY)
				{
					pd.position.y = groundY;
					pd.velocity.y = -pd.velocity.y;
					pd.velocity *= bounceDamping;
				}

				// ----- Render update -----
				customParticleSystem.UpdateParticle(pd.particle, pd.position, radius, pd.color);
			}
		}
	}
}

