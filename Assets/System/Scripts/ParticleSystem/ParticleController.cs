using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
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
		public Material particleMaterial;
		public Color color = Color.white;
		public float radius = 0.02f;
		[Range(0f, 1f)] public float fadeStartTime = 1f;

		[Header("Physics")]
		public bool enablePhysics = false;
		public float gravity = 10f;
		[Range(0f, 1f)] public float airFriction = 0.01f;
		//public Vector3 velocityBias = Vector3.zero;
		//public Vector3 velocityMagnitude = Vector3.one;
		public MinMaxRange velocityScalarRange = new MinMaxRange(1f, 1f);
		[Tooltip("Apex spread angle in degrees.\n0 = no spread (straight)\n90 = ±45° cone\n180 = full hemisphere\n360 = full sphere")]
		[Range(0f, 360f)]
		public float spreadApexAngle = 20f;

		public bool enableCollision = false;
		public float groundHeight = 0f;
		public float bounceFriction = 0.2f;

		[Header("Floater Behaviour")]
		public bool enableFloater = false;
		public float floaterDriftAmplitude = 1.5f;
		public float floaterDriftFrequency = 0.3f;
		[Tooltip("Controls spatial coherence: higher = larger coherent groups")]
		public float floaterSpatialScale = 0.1f;

		[Header("Animation")]
		public AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

		[Header("PWM / Emission")]
		[Range(1, 128)] public int particleCount = 1;
		public Vector3 scatterScalar = Vector3.zero;
		[Range(0.1f, 10f)] public float cycleTime = 0.1f;
		public List<Pulse> pulses = new() { new Pulse { start = 0f, end = 0.1f } };

		[System.Serializable]
		public class Pulse
		{
			[Range(0f, 1f)] public float start;
			[Range(0f, 1f)] public float end;
		}

		// === SHARED SCALE TABLE ===
		[SerializeField, HideInInspector] private ScaleTable _sharedScaleTable;
		private const int ScaleTableResolution = 32;

		public ScaleTable SharedScaleTable => _sharedScaleTable;

		[Header("Timeline Randomisation")]
		public bool randomiseTimelineOffset = true;
		[Tooltip("0 = non-deterministic, otherwise stable per-instance")]
		public int timelineSeed = 0;

		public struct ScaleTable
		{
			public float[] values;
			public int resolution;

			public ScaleTable(float[] v, int r) { values = v; resolution = r; }

			[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
			public readonly float Evaluate(float norm)
			{
				int i = (int)(norm * (resolution - 1));
				i = Mathf.Clamp(i, 0, resolution - 2);
				float frac = norm * (resolution - 1f) - i;
				return Mathf.LerpUnclamped(values[i], values[i + 1], frac);
			}
		}

		public ParticleSystem customParticleSystem;
		private bool forceEmission = false;
		private float timelinePosition = 0f;
		private float lastTimelinePosition = 0f;

		private readonly int _debugActiveCount = 0;
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

			RebakeScaleTable();
		}

		public void RebakeScaleTable()
		{
			float[] values = new float[ScaleTableResolution];
			for (int i = 0; i < ScaleTableResolution; i++)
			{
				float t = i / (float)(ScaleTableResolution - 1);
				values[i] = scaleCurve.Evaluate(t);
			}
			_sharedScaleTable = new ScaleTable(values, ScaleTableResolution);
		}

		private void OnEnable()
		{
			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

			if (randomiseTimelineOffset && cycleTime > 0f)
			{
				if (timelineSeed != 0)
					Random.InitState(timelineSeed ^ GetHashCode());

				timelinePosition = Random.value * cycleTime;
				lastTimelinePosition = timelinePosition; // CRITICAL
			}
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
			if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out var _)) return;

			customParticleSystem.Render(cam);
		}

		private void FixedUpdate()
		{
			if (!updateParticles || customParticleSystem == null) return;

			customParticleSystem.UpdateParticles(Time.deltaTime);

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

			// Base direction (local "up" in world space)
			Vector3 baseDirection = (transform.rotation * Vector3.up).normalized;

			// Random speed from the scalar range
			float speed = Random.Range(velocityScalarRange.min, velocityScalarRange.max);

			// === SPREAD CONE LOGIC ===
			Vector3 finalDirection;

			if (spreadApexAngle <= 0.01f)
			{
				// Zero spread - perfectly straight
				finalDirection = baseDirection;
			}
			else if (spreadApexAngle >= 360f)
			{
				// Full sphere - completely random direction
				finalDirection = Random.onUnitSphere;
			}
			else
			{
				float maxHalfAngleDeg = spreadApexAngle * 0.5f;

				if (maxHalfAngleDeg > 180f)
				{
					// Exclude a cone on the negative side (for angles > 180°)
					Vector3 randomDir = Random.onUnitSphere;
					float excludeCos = Mathf.Cos((360f - spreadApexAngle) * 0.5f * Mathf.Deg2Rad);

					// Rejection sampling: keep sampling until we are outside the excluded cone
					while (Vector3.Dot(randomDir, -baseDirection) > excludeCos)
					{
						randomDir = Random.onUnitSphere;
					}
					finalDirection = randomDir;
				}
				else
				{
					// Standard cone spread (0° to 180°)
					finalDirection = GetRandomDirectionInCone(baseDirection, maxHalfAngleDeg);
				}
			}

			// Final velocity = direction * speed + bias
			Vector3 velocity = finalDirection * speed;// + velocityBias;

			float variation = lifetimeVariation;
			float life = lifetime + Random.Range(-variation, variation);
			if (life <= 0f) return;

			Particle p = customParticleSystem.AllocateParticle();
			if (p == null) return;

			p.duration = life;
			p.life = life;
			p.position = position;
			p.oldPosition = position;
			p.radius = radius;
			p.color = color;

			// ------------------------------------------------------------
			// 1. ALWAYS add colour & scale behaviours
			// ------------------------------------------------------------
			var colourBh = new ParticleBehaviourColour { fadeStartTime = fadeStartTime };
			var scaleBh = new ParticleBehaviourScale { scaleTable = _sharedScaleTable, initialRadius = radius };

			p.behaviours.Add(colourBh);
			p.behaviours.Add(scaleBh);

			// ------------------------------------------------------------
			// 2. OPTIONAL physics
			// ------------------------------------------------------------
			if (enablePhysics)
			{
				p.behaviours.Add(new ParticlePhysicsBehaviour
				{
					velocity = velocity,
					gravity = this.gravity,
					friction = this.airFriction
				});

				if (enableCollision)
				{
					p.behaviours.Add(new ParticleGroundCollisionBehaviour
					{
						friction = bounceFriction,
						groundHeight = groundHeight
					});
				}
			}

			// ------------------------------------------------------------
			// 3. (Future) add any extra custom behaviours here
			// ------------------------------------------------------------

			p.Initialize();               // runs Initialize() on every behaviour
		}

		/// <summary>
		/// Returns a uniformly distributed random unit vector inside a cone.
		/// maxHalfAngleDeg = half the apex angle (spreadApexAngle / 2).
		/// </summary>
		private static Vector3 GetRandomDirectionInCone(Vector3 direction, float maxHalfAngleDeg)
		{
			float maxHalfAngleRad = maxHalfAngleDeg * Mathf.Deg2Rad;

			// Uniform distribution on spherical cap
			float z = Random.Range(Mathf.Cos(maxHalfAngleRad), 1f);
			float theta = Random.Range(0f, Mathf.PI * 2f);
			float r = Mathf.Sqrt(1f - z * z);

			Vector3 localDir = new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z);

			// Rotate from cone space (Z-forward) to world direction
			Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
			return rotation * localDir;
		}

		//public void SpawnParticle()
		//{
		//	if (!updateParticles || customParticleSystem == null) return;

		//	Vector3 position = transform.position + EllipsoidRandom.Inside(scatterScalar);
		//	//Vector3 velocity = velocityBias + EllipsoidRandom.Inside(velocityMagnitude);
		//	//Vector3 direction = transform.rotation * Vector3.up * velocityMagnitude.magnitude;
		//	//Vector3 velocity = direction;// + velocityBias + EllipsoidRandom.Inside(velocityMagnitude);

		//	// === NEW VELOCITY SYSTEM USING SCALAR RANGE ===
		//	Vector3 baseDirection = transform.rotation * Vector3.up;

		//	// Pick a random speed between min and max
		//	float speed = Random.Range(velocityScalarRange.min, velocityScalarRange.max);

		//	// Create final velocity: direction * random speed + bias
		//	Vector3 velocity = baseDirection * speed + velocityBias;

		//	float variation = lifetimeVariation;
		//	float life = lifetime + Random.Range(-variation, variation);
		//	if (life <= 0f) return;

		//	Particle p = customParticleSystem.AllocateParticle();
		//	if (p == null) return;

		//	p.duration = life;
		//	p.life = life;
		//	p.position = position;
		//	p.oldPosition = position;
		//	p.radius = radius;
		//	p.color = color;

		//	// ------------------------------------------------------------
		//	// 1. ALWAYS add colour & scale behaviours
		//	// ------------------------------------------------------------
		//	var colourBh = new ParticleBehaviourColour { fadeStartTime = fadeStartTime };
		//	var scaleBh = new ParticleBehaviourScale { scaleTable = _sharedScaleTable, initialRadius = radius };

		//	p.behaviours.Add(colourBh);
		//	p.behaviours.Add(scaleBh);

		//	// ------------------------------------------------------------
		//	// 2. OPTIONAL physics
		//	// ------------------------------------------------------------
		//	if (enablePhysics)
		//	{
		//		p.behaviours.Add(new ParticlePhysicsBehaviour
		//		{
		//			velocity = velocity,
		//			gravity = this.gravity,
		//			friction = this.airFriction
		//		});

		//		if (enableCollision)
		//		{
		//			p.behaviours.Add(new ParticleGroundCollisionBehaviour
		//			{
		//				friction = bounceFriction,
		//				groundHeight = groundHeight
		//			});
		//		}
		//	}

		//	// ------------------------------------------------------------
		//	// 3. (Future) add any extra custom behaviours here
		//	// ------------------------------------------------------------

		//	p.Initialize();               // runs Initialize() on every behaviour
		//}

#if UNITY_EDITOR
		public Material ParticleMaterial => particleMaterial;

		private void OnRenderObject()
		{
			if (!showInSceneView) return;
			if (customParticleSystem == null) return;
			if (Camera.current == null) return;
			if (Camera.current.cameraType != CameraType.SceneView) return;
			if (!SceneView.currentDrawingSceneView) return;
			ParticleControllerSceneView.OnRender(this);
		}
#endif

		private void OnValidate()
		{
			if (Application.isPlaying)
				RebakeScaleTable();
		}

		///// <summary>
		///// Returns a uniformly distributed random unit vector inside a cone.
		///// maxHalfAngleDeg = half the apex angle (e.g. spreadApexAngle / 2).
		///// </summary>
		//private static Vector3 GetRandomDirectionInCone(Vector3 direction, float maxHalfAngleDeg)
		//{
		//	float maxHalfAngleRad = maxHalfAngleDeg * Mathf.Deg2Rad;

		//	// Uniform distribution on the spherical cap
		//	float z = Random.Range(Mathf.Cos(maxHalfAngleRad), 1f);   // z in local cone space
		//	float theta = Random.Range(0f, Mathf.PI * 2f);            // azimuthal angle
		//	float r = Mathf.Sqrt(1f - z * z);

		//	Vector3 localDir = new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z);

		//	// Rotate localDir from cone space (Z = forward) to world space aligned with 'direction'
		//	if (Vector3.Dot(Vector3.forward, direction) > 0.9999f)
		//		return localDir; // already aligned with Z

		//	Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
		//	return rotation * localDir;
		//}
	}
}