using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	public class ParticleController : MonoBehaviour
	{
		// ──────────────────────────────────────────────────────────────
		// (all inspector fields – copy from your previous version)
		// ──────────────────────────────────────────────────────────────
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

		[System.Serializable]
		public class Pulse
		{
			[Range(0f, 1f)] public float start;
			[Range(0f, 1f)] public float end;
		}

		[SerializeField] private Material particleMaterial;

		private ParticleSystem customParticleSystem;
		private bool emitEnabled;
		private float timelinePosition;
		private float lastTimelinePosition;

		private void Awake()
		{
			if (particleMaterial == null)
			{
				Debug.LogError("ParticleController: particleMaterial is not assigned!");
				enabled = false;
				return;
			}

			if (scaleCurve.keys.Length == 0)
				scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

			customParticleSystem = new ParticleSystem(particleMaterial, useThreeZoneSlicing, this);
			timelinePosition = lastTimelinePosition = 0f;
			emitEnabled = false;
		}

		private void FixedUpdate()
		{
			if (!updateParticles || customParticleSystem == null) return;

			customParticleSystem.UpdateParticles();

			lastTimelinePosition = timelinePosition;
			timelinePosition += Time.deltaTime;
			if (timelinePosition >= cycleTime)
				timelinePosition -= cycleTime;

			float normNow = timelinePosition / cycleTime;
			float normPrev = lastTimelinePosition / cycleTime;
			bool inPulse = false;

			foreach (var p in pulses)
			{
				if (normNow >= p.start && normNow <= p.end) inPulse = true;

				bool crossed = false;
				if (normPrev <= normNow)
				{
					if (normPrev < p.end && normNow > p.start) crossed = true;
				}
				else
				{
					if (normPrev < p.end || normNow > p.start) crossed = true;
				}

				if (crossed) EmitParticlesInternal();
			}

			if (emitEnabled || inPulse) EmitParticlesInternal();
		}

		private void Update()
		{
			customParticleSystem?.Render();
		}

		public void EmitParticles() { emitEnabled = true; timelinePosition = 0f; }
		public void StopEmitting() { emitEnabled = false; timelinePosition = 0f; }

		private void EmitParticlesInternal()
		{
			if (customParticleSystem == null) return;

			int cnt = Mathf.Max(1, particleCount);
			for (int i = 0; i < cnt; ++i)
			{
				Vector3 scatterVec = Random.value * scatter * Random.onUnitSphere;
				SpawnParticle(transform.position, velocity + scatterVec);
			}
		}

		public void SpawnParticle(Vector3 position, Vector3 velocity, float? lifetimeVariation = null)
		{
			if (!updateParticles || customParticleSystem == null) return;

			float variation = lifetimeVariation ?? this.lifetimeVariation;
			float life = lifetime + Random.Range(-variation, variation);
			if (life <= 0f) return;

			Particle p = null;

			if (gravity > 0.01f || bounceDamping < 0.99f)
			{
				var pp = customParticleSystem.AllocateParticle<PhysicsParticle>();
				if (pp == null) return;

				pp.duration = life;
				pp.life = life;
				pp.position = position;
				pp.initialRadius = radius;
				pp.radius = radius;
				pp.color = color;
				pp.velocity = velocity;
				pp.gravity = gravity;
				pp.bounceDamping = bounceDamping;
				pp.groundHeight = groundHeight;
				pp.useGlobalGroundPlane = useGlobalGroundPlane;
				p = pp;
			}
			else
			{
				var sp = customParticleSystem.AllocateParticle<StaticParticle>();
				if (sp == null) return;

				sp.duration = life;
				sp.life = life;
				sp.position = position;
				sp.initialRadius = radius;
				sp.radius = radius;
				sp.color = color;
				p = sp;
			}

			// ---- First-frame update (still zero-allocation) ----
			var ctx = new ParticleUpdateContext
			{
				controller = this,
				deltaTime = 0f,
				normalizedLife = 0f
			};
			p.Update(ref ctx);
		}
	}
}