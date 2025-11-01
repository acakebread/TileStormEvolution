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
		[SerializeField] private Material particleMaterial;
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

		private ParticleSystem customParticleSystem;
		private bool forceEmission = false;
		private float timelinePosition = 0f;
		private float lastTimelinePosition = 0f;

		// Debug GUI tracking
		private int lastViewCount = 0;

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
			RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
		}

		private void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
		}

		private void OnBeginCameraRendering(ScriptableRenderContext _, Camera cam)
		{
			if (customParticleSystem == null) return;

			var uacd = cam.GetComponent<UniversalAdditionalCameraData>();
			if (uacd == null) return;

			customParticleSystem.Render(cam);
		}

		private void OnEndCameraRendering(ScriptableRenderContext _, Camera cam)
		{
			// Only update debug count after the *last* camera in the frame
			// We don't know which is last, so we update every time — it's fine.
			if (customParticleSystem != null)
				lastViewCount = customParticleSystem.ViewCount;
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
		private void OnRenderObject()
		{
			if (!showInSceneView) return;
			if (customParticleSystem == null) return;
			if (Camera.current == null) return;
			if (Camera.current.cameraType != CameraType.SceneView) return;

#if UNITY_EDITOR
			if (!UnityEditor.SceneView.currentDrawingSceneView) return;
#endif

			var mesh = customParticleSystem.GetDebugMesh();
			if (mesh == null) return;

			var mat = useDebugMaterial ? GetCyanDebugMaterial() : particleMaterial;
			if (mat == null) return;

			if (useDebugMaterial)
			{
				EnsureWhiteColors(mesh);
			}

			mat.SetPass(0);
			Graphics.DrawMeshNow(mesh, transform.localToWorldMatrix);
		}

		private static Material cyanDebugMaterial;
		private static Color[] whiteColorCache;
		private static int lastVertexCount = 0;

		private Material GetCyanDebugMaterial()
		{
			if (cyanDebugMaterial != null) return cyanDebugMaterial;

			var shader = Shader.Find("Debug/TriggerWireframe");
			if (shader == null)
			{
				Debug.LogWarning("Debug/TriggerWireframe not found. Using Unlit/Color.");
				shader = Shader.Find("Unlit/Color");
			}

			cyanDebugMaterial = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave,
				color = new Color(0, 1, 1, 0.3f)
			};

			return cyanDebugMaterial;
		}

		private void EnsureWhiteColors(Mesh mesh)
		{
			int vertexCount = mesh.vertexCount;
			if (whiteColorCache == null || whiteColorCache.Length != vertexCount)
			{
				whiteColorCache = new Color[vertexCount];
				for (int i = 0; i < vertexCount; i++)
					whiteColorCache[i] = Color.white;
				lastVertexCount = vertexCount;
			}
			else if (lastVertexCount != vertexCount)
			{
				System.Array.Resize(ref whiteColorCache, vertexCount);
				for (int i = lastVertexCount; i < vertexCount; i++)
					whiteColorCache[i] = Color.white;
				lastVertexCount = vertexCount;
			}

			mesh.colors = whiteColorCache;
		}

		private void OnDestroy()
		{
			if (cyanDebugMaterial != null)
			{
				if (Application.isPlaying)
					Object.Destroy(cyanDebugMaterial);
				else
					Object.DestroyImmediate(cyanDebugMaterial);
				cyanDebugMaterial = null;
			}
		}
#endif

		// ================================================================
		// DEBUG GUI: Shows ParticleMesh slots used & active particle count
		// ================================================================
		//#if UNITY_EDITOR || DEVELOPMENT_BUILD
		//		private void OnGUI()
		//		{
		//			if (!Application.isPlaying && !showInSceneView) return;

		//			GUI.color = Color.cyan;
		//			GUI.skin.label.fontStyle = FontStyle.Bold;
		//			GUI.skin.label.fontSize = 12;

		//			GUILayout.BeginArea(new Rect(15, 15, 400, 100), GUI.skin.box);
		//			GUILayout.Label("<color=white><b>PARTICLE SYSTEM DEBUG</b></color>");
		//			GUILayout.Label($"<color=yellow>ParticleMesh slots used:</color> <color=white>{lastViewCount}</color>/<color=lime>{ParticleSystem.MaxViewCache}</color>");
		//			GUILayout.Label($"<color=yellow>Active particles:</color> <color=white>{(customParticleSystem?.ActiveParticleCount ?? 0)}</color>");
		//			GUILayout.EndArea();
		//		}
		//#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
		private int stableViewCount = 0;
		private float lastGuiUpdate = 0f;
		private const float GuiUpdateInterval = 0.1f; // Update GUI 10 times/sec

		private void OnGUI()
		{
			if (!Application.isPlaying && !showInSceneView) return;

			// Throttle GUI updates to reduce flicker
			float now = Time.unscaledTime;
			if (now - lastGuiUpdate >= GuiUpdateInterval)
			{
				stableViewCount = lastViewCount;
				lastGuiUpdate = now;
			}

			GUI.color = Color.cyan;
			GUI.skin.label.fontStyle = FontStyle.Bold;
			GUI.skin.label.fontSize = 12;

			GUILayout.BeginArea(new Rect(15, 15, 400, 100), GUI.skin.box);
			GUILayout.Label("<color=white><b>PARTICLE SYSTEM DEBUG</b></color>");
			GUILayout.Label($"<color=yellow>ParticleMesh slots used:</color> <color=white>{stableViewCount}</color>/<color=lime>{ParticleSystem.MaxViewCache}</color>");
			GUILayout.Label($"<color=yellow>Active particles:</color> <color=white>{(customParticleSystem?.ActiveParticleCount ?? 0)}</color>");
			GUILayout.EndArea();
		}
#endif
	}
}