using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	// --------------------------------------------------------------------
	// Re-usable update context – **zero allocations per particle**
	// --------------------------------------------------------------------
	public struct ParticleUpdateContext
	{
		public ParticleController controller;
		public float deltaTime;
		public float normalizedLife;
	}

	// --------------------------------------------------------------------
	// Base particle
	// --------------------------------------------------------------------
	public abstract class Particle
	{
		public float life;
		public float duration;
		public float initialRadius;
		public int vertexIndex;
		public int poolIndex;
		public Vector3 position;
		public Vector3 delta;
		public float radius;
		public Color color;

		public abstract void Update(ref ParticleUpdateContext ctx);
	}

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

	// --------------------------------------------------------------------
	// ParticleSystem – pool / mesh / update / render
	// --------------------------------------------------------------------
	public class ParticleSystem
	{
		private const int MaxParticles = 8192;
		private readonly bool useThreeZoneSlicing;
		private readonly Material material;
		private readonly Transform cameraTransform;
		private readonly int verticesPerParticle;

		private readonly List<Particle> particlePool = new List<Particle>(MaxParticles);
		public readonly List<Particle> activeParticles = new List<Particle>(MaxParticles);
		private readonly List<int> freeParticleIndices = new List<int>(MaxParticles);

		private Mesh mesh;
		private readonly List<Vector3> vertices;
		private readonly List<int> triangles;
		private readonly List<Color> colors;
		private readonly List<Vector2> uvs;

		public ParticleController Controller { get; private set; }

		// ----------------------------------------------------------------
		// **Mutable** reusable context – NOT readonly
		// ----------------------------------------------------------------
		private ParticleUpdateContext updateCtx;

		// ----------------------------------------------------------------
		public ParticleSystem(Material particleMaterial, bool threeZoneSlicing, ParticleController controller)
		{
			material = new Material(particleMaterial);
			useThreeZoneSlicing = threeZoneSlicing;
			verticesPerParticle = useThreeZoneSlicing ? 8 : 4;
			cameraTransform = Camera.main.transform;
			Controller = controller;

			int trianglesPerParticle = useThreeZoneSlicing ? 18 : 6;
			int totalTriangles = MaxParticles * trianglesPerParticle;

			vertices = new List<Vector3>(MaxParticles * 8);
			triangles = new List<int>(totalTriangles);
			colors = new List<Color>(MaxParticles * 8);
			uvs = new List<Vector2>(MaxParticles * 8);

			InitializePool();
			InitializeMesh();
			SetupURPMaterial();

			// initialise the reusable context once
			updateCtx = new ParticleUpdateContext();
		}

		// ----------------------------------------------------------------
		private void SetupURPMaterial()
		{
			if (material.shader.name != "MassiveHadronLtd/Unlit/AdditiveParticles")
			{
				var s = Shader.Find("MassiveHadronLtd/Unlit/AdditiveParticles");
				if (s) material.shader = s;
			}
			material.SetColor("_BaseColor", Color.white);
			material.SetFloat("_ZWrite", 0);
			material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;
		}

		// ----------------------------------------------------------------
		private void InitializePool()
		{
			for (int i = 0; i < MaxParticles; i++)
			{
				particlePool.Add(null);
				freeParticleIndices.Add(i);
			}
		}

		// ----------------------------------------------------------------
		private void InitializeMesh()
		{
			mesh = new Mesh { name = "ParticleMesh" };
			mesh.MarkDynamic();

			for (int i = 0; i < MaxParticles; i++)
			{
				int offset = i * verticesPerParticle;
				if (useThreeZoneSlicing)
				{
					vertices.AddRange(new Vector3[8]);
					triangles.AddRange(new[]
					{
						offset+0,offset+1,offset+2, offset+2,offset+1,offset+3,
						offset+4,offset+2,offset+3, offset+4,offset+3,offset+5,
						offset+6,offset+4,offset+5, offset+6,offset+5,offset+7
					});
					colors.AddRange(new Color[8]);
					uvs.AddRange(new[]
					{
						new Vector2(0,1), new Vector2(1,1), new Vector2(0,0.5f), new Vector2(1,0.5f),
						new Vector2(0,0.5f), new Vector2(1,0.5f), new Vector2(0,0), new Vector2(1,0)
					});
				}
				else
				{
					vertices.AddRange(new Vector3[4]);
					triangles.AddRange(new[] { offset + 0, offset + 1, offset + 2, offset + 1, offset + 3, offset + 2 });
					colors.AddRange(new Color[4]);
					uvs.AddRange(new[] { new Vector2(0,1), new Vector2(1,1), new Vector2(0,0), new Vector2(1,0)});
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.SetColors(colors);
			mesh.SetUVs(0, uvs);
			mesh.RecalculateBounds();
		}

		// ----------------------------------------------------------------
		public T AllocateParticle<T>() where T : Particle, new()
		{
			if (freeParticleIndices.Count == 0)
			{
				return null;
			}

			int idx = freeParticleIndices[freeParticleIndices.Count - 1];
			freeParticleIndices.RemoveAt(freeParticleIndices.Count - 1);

			var p = new T
			{
				vertexIndex = idx * verticesPerParticle,
				poolIndex = idx,
				life = -1f,
				position = Vector3.zero,
				delta = Vector3.zero,
				radius = 0f,
				color = Color.clear
			};
			particlePool[idx] = p;
			activeParticles.Add(p);
			return p;
		}

		// ----------------------------------------------------------------
		// **ZERO allocations** – one struct reused for the whole frame
		// ----------------------------------------------------------------
		public void UpdateParticles()
		{
			if (Controller == null) return;

			float dt = Time.deltaTime;

			// fill once
			updateCtx.controller = Controller;
			updateCtx.deltaTime = dt;

			for (int i = activeParticles.Count - 1; i >= 0; i--)
			{
				Particle p = activeParticles[i];
				if (p.life <= 0f) continue;

				p.life -= dt;
				if (p.life <= 0f)
				{
					DeactivateParticle(i);
					continue;
				}

				updateCtx.normalizedLife = 1f - (p.life / p.duration);
				p.Update(ref updateCtx);
			}
		}

		// ----------------------------------------------------------------
		private void DeactivateParticle(int index)
		{
			Particle p = activeParticles[index];
			int v = p.vertexIndex;
			for (int i = 0; i < verticesPerParticle; i++)
			{
				vertices[v + i] = Vector3.zero;
				colors[v + i] = Color.clear;
			}
			freeParticleIndices.Add(p.poolIndex);
			activeParticles.RemoveAt(index);
		}

		// ----------------------------------------------------------------
		public void Render()
		{
			UpdateMesh();
			Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0);
		}

		private void UpdateMesh()
		{
			Vector3 camPos = cameraTransform ? cameraTransform.transform.position : Vector3.zero;

			for (int i = 0; i < activeParticles.Count; i++)
			{
				Particle p = activeParticles[i];
				if (p.life <= 0f) continue;

				Vector3 pos = p.position;
				Vector3 delta = p.delta;
				Vector3 toCam = (pos - camPos).normalized;

				// ──────────────────────────────────────────────────────────────
				// TANGENT: moving → from delta, stationary → from camera up
				// ──────────────────────────────────────────────────────────────
				Vector3 tangent;
				if (delta.sqrMagnitude > 0.000001f)
				{
					tangent = Vector3.Cross(delta, toCam).normalized;
				}
				else
				{
					Vector3 camUp = cameraTransform ? cameraTransform.transform.up : Vector3.up;
					tangent = Vector3.Cross(camUp, toCam).normalized;
					if (tangent.sqrMagnitude < 0.01f)
						tangent = Vector3.Cross(Vector3.up, toCam).normalized;
				}
				// ──────────────────────────────────────────────────────────────

				float dot = Vector3.Dot(delta, toCam);
				float tang = (delta - dot * toCam).magnitude;
				if (tang < p.radius)
				{
					Vector3 cross = Vector3.Cross(toCam, tangent);
					delta += (p.radius - tang) * cross;
				}

				Vector3 head = pos + delta;
				Vector3 tail = pos - delta;
				Vector3 rad = tangent * p.radius;
				int v = p.vertexIndex;

				if (useThreeZoneSlicing)
				{
					float velComp = tang > 0.0001f ? Mathf.Max(0, tang - p.radius) / tang : 0f;
					Vector3 half = velComp * delta;
					Vector3 headB = pos + half;
					Vector3 tailB = pos - half;

					vertices[v + 0] = head - rad; vertices[v + 1] = head + rad;
					vertices[v + 2] = headB - rad; vertices[v + 3] = headB + rad;
					vertices[v + 4] = tailB - rad; vertices[v + 5] = tailB + rad;
					vertices[v + 6] = tail - rad; vertices[v + 7] = tail + rad;
					for (int j = 0; j < 8; j++) colors[v + j] = p.color;
				}
				else
				{
					vertices[v + 0] = head - rad; vertices[v + 1] = head + rad;
					vertices[v + 2] = tail - rad; vertices[v + 3] = tail + rad;
					for (int j = 0; j < 4; j++) colors[v + j] = p.color;
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.RecalculateBounds();
		}
	}
}