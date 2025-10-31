using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
			velocity *= 1f - friction;//ToDo calculate air friction properly - frame rate independant
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

	[ExecuteInEditMode]  // ← CRITICAL
	public class ParticleController : MonoBehaviour
	{
		// ──────────────────────────────────────────────────────────────
		// (all inspector fields – copy from your previous version)
		// ──────────────────────────────────────────────────────────────
		[Header("Debug")]//[Header("Rendering")]
		public bool showInSceneView = true;
		public bool updateParticles = true;

		[Header("Lifetime")]
		public float lifetime = 1f;
		public float lifetimeVariation = 0.5f;

		[Header("Appearance")]
		public bool useThreeZoneSlicing = false;
		[SerializeField] private Material particleMaterial;
		public Color color = Color.white;
		public float radius = 0.02f;
		[Range(0f, 1f)] public float fadeStartTime = 1f;//[Header("Fade")]

		[Header("Physics")]
		public float gravity = 10f;
		[Range(0f, 1f)] public float friction = 0.01f;
		public Vector3 velocityBias = Vector3.zero;
		public Vector3 velocityMagnitude = Vector3.one;
		public bool enableCollision = false;
		public float groundHeight = 0f;
		public float bounceDamping = 0.8f;

		[Header("Animation")]//for some reason this doesn't display - editor interfering
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

		private ParticleSystem customParticleSystem;
		private bool forceEmission = false;//UI override for continuous emission
		private float timelinePosition = 0f;
		private float lastTimelinePosition = 0f;

		private void Awake()
		{
			if (particleMaterial == null)
			{
				Debug.LogError("ParticleController: particleMaterial is not assigned!");
				enabled = false;
				return;
			}

			if (scaleCurve.keys.Length == 0)
				scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);//default

			customParticleSystem = new ParticleSystem(particleMaterial, useThreeZoneSlicing, this);
			// Remove the unnecessary if(Application.isPlaying) - the creation is the same for edit/play mode
		}

		private void OnEnable()
		{
			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		}

		private void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
		}

		/* --------------------------------------------------------------
           This is the ONLY place we draw particles.
           It is called once for every camera that URP renders.
           -------------------------------------------------------------- */
		private void OnBeginCameraRendering(ScriptableRenderContext _, Camera cam)
		{
			//Debug.Log($"[Particle] Rendering for camera: {cam.name} (tag: {cam.tag})");
			if (customParticleSystem == null) return;

			// ---- 1. Skip cameras that are not part of the reflection stack ----
			// (main camera, reflectionCamera and textureCamera all have a
			//  UniversalAdditionalCameraData component – everything else can be ignored)
			var uacd = cam.GetComponent<UniversalAdditionalCameraData>();
			if (uacd == null) return;

			// ---- 2. OPTIONAL: filter by tag if you want to be extra safe ----
			// (uncomment if you added the tags in ReflectionEffectCamera)
			// if (!cam.CompareTag("MainCamera") &&
			//     !cam.CompareTag("ReflectionCamera") &&
			//     !cam.CompareTag("TextureCamera")) return;

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
			{
				Vector3 position = transform.position + EllipsoidRandom.Inside(scatterScalar);
				Vector3 veolcity = velocityBias + EllipsoidRandom.Inside(velocityMagnitude);
				SpawnParticle(position, veolcity);
			}
		}

		public void SpawnParticle(Vector3 position, Vector3 velocity, float? lifetimeVariation = null)
		{
			if (!updateParticles || customParticleSystem == null) return;

			float variation = lifetimeVariation ?? this.lifetimeVariation;
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

			// ---- First-frame update (still zero-allocation) ----
			var ctx = new ParticleUpdateContext
			{
				controller = this,
				deltaTime = 0f,
				normalizedLife = 0f
			};
			p.Update(ref ctx);
		}

		// ----------------------------------------------------------------
		// SCENE VIEW DEBUG RENDER
		// ----------------------------------------------------------------
#if UNITY_EDITOR
		private void OnRenderObject()
		{
			if (!showInSceneView || customParticleSystem == null) return;
			if (Camera.current == null) return;
			if (Camera.current.cameraType != CameraType.SceneView) return;

			var mesh = customParticleSystem.GetDebugMesh();
			if (mesh == null) return;

			var mat = GetDebugMaterial();
			if (mat == null) return;

			// Draw both passes
			for (int pass = 0; pass < mat.passCount; pass++)
			{
				mat.SetPass(pass);
				Graphics.DrawMeshNow(mesh, transform.localToWorldMatrix);
			}
		}

		private static Material _debugMat;
		private Material GetDebugMaterial()
		{
			if (_debugMat != null) return _debugMat;

			var shader = Shader.Find("Debug/ParticleOutlineSimple");
			if (shader == null)
			{
				Debug.LogWarning("Debug/ParticleOutlineSimple not found. Using fallback.");
				shader = Shader.Find("Unlit/Color");
			}

			_debugMat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave
			};

			if (shader.name.Contains("OutlineSimple"))
			{
				_debugMat.SetColor("_MainColor", new Color(0, 1, 1, 0.3f));
				_debugMat.SetColor("_OutlineColor", new Color(1, 0, 1, 1));
				_debugMat.SetFloat("_OutlineWidth", 0.2f); // 20% larger
			}
			else
			{
				_debugMat.color = new Color(1, 0, 1, 0.3f);
			}

			return _debugMat;
		}
#endif
	}
}