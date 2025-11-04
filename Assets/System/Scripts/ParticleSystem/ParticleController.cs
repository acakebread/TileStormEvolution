using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	// --------------------------------------------------------------------
	// Physics particle
	// --------------------------------------------------------------------
	public class PhysicsParticle : Particle
	{
		public Vector3 velocity;
		public float gravity;
		public float friction;
		public float bounceDamping;
		public float groundHeight;
		public bool enableCollision;

		public override void Update(ref ParticleUpdateContext ctx)
		{
			float norm = ctx.normalizedLife;

			// fade
			float a = (norm < ctx.controller.fadeStartTime || Mathf.Approximately(ctx.controller.fadeStartTime, 1f))
				? 1f
				: Mathf.Clamp01(1f - ((norm - ctx.controller.fadeStartTime) / (1f - ctx.controller.fadeStartTime)));
			color.a = a;

			// scale
			radius = initialRadius * ctx.controller.scaleCurve.Evaluate(norm);

			// physics
			velocity.y -= gravity * ctx.deltaTime;
			velocity *= 1f - friction;
			if (enableCollision) position.y = Mathf.Max(position.y, groundHeight);
			delta = -position;
			position += velocity * ctx.deltaTime;

			if (true == enableCollision && velocity.y < 0f && position.y <= groundHeight)
			{
				position.y = groundHeight;
				velocity.y = -velocity.y * bounceDamping;
			}

			delta += position; // newPos – oldPos
		}
	}

	// --------------------------------------------------------------------
	// Static particle – **billboard fallback**
	// --------------------------------------------------------------------
	public class StaticParticle : Particle
	{
		public override void Update(ref ParticleUpdateContext ctx)
		{
			float norm = ctx.normalizedLife;

			// fade
			float a = (norm < ctx.controller.fadeStartTime || Mathf.Approximately(ctx.controller.fadeStartTime, 1f))
				? 1f
				: Mathf.Clamp01(1f - ((norm - ctx.controller.fadeStartTime) / (1f - ctx.controller.fadeStartTime)));
			color.a = a;

			// scale
			radius = initialRadius * ctx.controller.scaleCurve.Evaluate(norm);
		}
	}

	[ExecuteInEditMode]
	public class ParticleController : MonoBehaviour
	{
		[Header("Debug")]
		public bool showInSceneView = true;
		[Tooltip("True = Cyan debug (no tint), False = Real material (UVs/textures)")]
		public bool useDebugMaterial = false;
		public bool updateParticles = true;

		[Header("Lifetime")]
		public float lifetime = 1f;
		public float lifetimeVariation = 0.5f;

		[Header("Appearance")]
		public bool useThreeZoneSlicing = false;
		[SerializeField] public Material particleMaterial;
		public Color color = Color.white;
		public float radius = 0.02f;
		[Range(0f, 1f)] public float fadeStartTime = 1f;

		[Header("Physics")]
		public float gravity = 10f;
		[Range(0f, 1f)] public float friction = 0.01f;
		public Vector3 velocityBias = Vector3.zero;
		public Vector3 velocityMagnitude = Vector3.one;
		public bool enableCollision = false;
		public float groundHeight = 0f;
		public float bounceDamping = 0.8f;

		[Header("Animation")]
		public AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

		[Header("PWM / Emission")]
		[Range(1, 128)] public int particleCount = 1;
		public Vector3 scatterScalar = Vector3.zero;

		[Range(0.1f, 10f)] public float cycleTime = 0.1f;
		[SerializeField] public List<Pulse> pulses = new List<Pulse> { new Pulse { start = 0f, end = 0.1f } };

		[System.Serializable]
		public class Pulse
		{
			[Range(0f, 1f)] public float start;
			[Range(0f, 1f)] public float end;
		}

		public ParticleSystem customParticleSystem;
		private bool forceEmission = false;
		private float timelinePosition = 0f;
		private float lastTimelinePosition = 0f;

		// external debug
		private int _debugActiveCount = 0;
		public int DebugActiveCount => _debugActiveCount;

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
		}

		private void OnEnable()
		{
			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		}

		private void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
		}

		private int _lastFrameReset = -1;

		private void OnBeginCameraRendering(ScriptableRenderContext _, Camera cam)
		{
			if (Time.frameCount != _lastFrameReset)
			{
				ParticleSystem.ResetGlobalTracking();
				_lastFrameReset = Time.frameCount;
			}

			if (customParticleSystem == null) return;
			var uacd = cam.GetComponent<UniversalAdditionalCameraData>();
			if (uacd == null) return;

			customParticleSystem.Render(cam);
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
			bool emit = forceEmission;

			foreach (var p in pulses)
			{
				if (normNow >= p.start && normNow <= p.end) emit = true;

				if (normPrev <= normNow)
				{
					if (normPrev < p.end && normNow > p.start) emit = true;
				}
				else
				{
					if (normPrev < p.end || normNow > p.start) emit = true;
				}
				if (emit) break;
			}

			if (emit) EmitParticlesInternal();
		}

		public void EmitParticles() { forceEmission = true; timelinePosition = 0f; }
		public void StopEmitting() { forceEmission = false; timelinePosition = 0f; }

		private void EmitParticlesInternal()
		{
			if (customParticleSystem == null) return;
			int cnt = Mathf.Max(1, particleCount);
			for (int i = 0; i < cnt; ++i)
				SpawnParticle();
		}

		public void SpawnParticle()
		{
			if (!updateParticles || customParticleSystem == null) return;

			Vector3 position = transform.position + EllipsoidRandom.Inside(scatterScalar);
			Vector3 velocity = velocityBias + EllipsoidRandom.Inside(velocityMagnitude);
			float variation = lifetimeVariation;
			float life = lifetime + Random.Range(-variation, variation);
			if (life <= 0f) return;

			Particle p = null;

			if (gravity != 0f || velocity != Vector3.zero)
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
				pp.friction = friction;
				pp.bounceDamping = bounceDamping;
				pp.groundHeight = groundHeight;
				pp.enableCollision = enableCollision;
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

			var ctx = new ParticleUpdateContext
			{
				controller = this,
				deltaTime = 0f,
				normalizedLife = 0f
			};
			p.Update(ref ctx);
		}


#if UNITY_EDITOR
		public Material ParticleMaterial => particleMaterial;

		private void OnRenderObject()
		{
			if (!showInSceneView) return;
			if (customParticleSystem == null) return;
			if (Camera.current == null) return;
			if (Camera.current.cameraType != CameraType.SceneView) return;

#if UNITY_EDITOR
			if (!SceneView.currentDrawingSceneView) return;
#endif
			ParticleControllerSceneView.OnRender(this);
		}
#endif
	}
}