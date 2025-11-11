using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	public class PhysicsParticleBehaviour : ParticleBehaviour
	{
		public Vector3 velocity;
		public float gravity;
		public float friction;
		public float bounceDamping;
		public float groundHeight;
		public bool enableCollision;

		public override void Update(ref ParticleUpdateContext ctx, Particle p)
		{
			velocity.y -= gravity * ctx.deltaTime;
			velocity *= 1f - friction;

			if (enableCollision)
				p.position.y = Mathf.Max(p.position.y, groundHeight);

			p.delta = -p.position;
			p.position += velocity * ctx.deltaTime;

			if (enableCollision && velocity.y < 0f && p.position.y <= groundHeight)
			{
				p.position.y = groundHeight;
				velocity.y = -velocity.y * bounceDamping;
			}

			p.delta += p.position;
		}
	}

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

			if (useThreeZoneSlicing)
				customParticleSystem = new ParticleSystemThreeSlice(particleMaterial, this);
			else
				customParticleSystem = new ParticleSystemQuad(particleMaterial, this);
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

			Particle p = customParticleSystem.AllocateParticle();
			if (p == null) return;

			p.duration = life;
			p.life = life;
			p.position = position;
			p.initialRadius = radius;
			p.radius = radius;
			p.color = color;

			// Only ThreeSlice supports physics
			if (p is ParticleThreeSlice && (gravity != 0f || velocity != Vector3.zero))
			{
				var phys = new PhysicsParticleBehaviour
				{
					velocity = velocity,
					gravity = gravity,
					friction = friction,
					bounceDamping = bounceDamping,
					groundHeight = groundHeight,
					enableCollision = enableCollision
				};
				p.behaviour = phys;
			}
			// ParticleQuad: no behaviour needed

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