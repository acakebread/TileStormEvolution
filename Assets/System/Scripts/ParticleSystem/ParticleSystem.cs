using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class ParticleSystem
	{
		private readonly int maxParticles = 4096;
		private readonly bool useThreeZoneSlicing;
		private readonly Material material;
		private Mesh mesh;
		private readonly Camera mainCamera;

		public class Particle
		{
			public int vertexIndex;
			public int poolIndex;
			public Vector3 position;
			public Vector3 previousPosition;
			public float lifetime;            // < 0 → dead
			public float maxLifetime;
			public Color color;
			public float radius;
			public float initialRadius;
		}

		private List<Particle> particlePool;
		private List<Particle> activeParticles;
		private List<int> freeParticleIndices;
		private List<Vector3> vertices;
		private List<int> triangles;
		private List<Color> colors;
		private List<Vector2> uvs;
		private int activeParticleCount;
		private readonly int verticesPerParticle;
		private readonly int trianglesPerParticle;

		public ParticleSystem(Material particleMaterial, bool threeZoneSlicing)
		{
			material = new Material(particleMaterial);
			useThreeZoneSlicing = threeZoneSlicing;
			verticesPerParticle = useThreeZoneSlicing ? 8 : 4;
			trianglesPerParticle = useThreeZoneSlicing ? 18 : 6;
			mainCamera = Camera.main;

			InitializePool();
			InitializeMesh();
			SetupURPMaterial();
		}

		private void SetupURPMaterial()
		{
			if (material.shader.name != "MassiveHadronLtd/Unlit/AdditiveParticles")
			{
				Debug.LogWarning($"ParticleSystem: Expected shader 'MassiveHadronLtd/Unlit/AdditiveParticles'.");
				Shader s = Shader.Find("MassiveHadronLtd/Unlit/AdditiveParticles");
				if (s) material.shader = s;
			}
			material.SetColor("_BaseColor", Color.white);
			material.SetFloat("_ZWrite", 0);
			material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;
		}

		private void InitializePool()
		{
			particlePool = new List<Particle>(maxParticles);
			activeParticles = new List<Particle>(maxParticles);
			freeParticleIndices = new List<int>(maxParticles);

			for (int i = 0; i < maxParticles; i++)
			{
				var p = new Particle
				{
					vertexIndex = i * verticesPerParticle,
					poolIndex = i,
					lifetime = -1f
				};
				particlePool.Add(p);
				freeParticleIndices.Add(i);
			}
			activeParticleCount = 0;
		}

		private void InitializeMesh()
		{
			mesh = new Mesh { name = "ParticleMesh" };
			mesh.MarkDynamic();

			vertices = new List<Vector3>(maxParticles * verticesPerParticle);
			triangles = new List<int>(maxParticles * trianglesPerParticle);
			colors = new List<Color>(maxParticles * verticesPerParticle);
			uvs = new List<Vector2>(maxParticles * verticesPerParticle);

			for (int i = 0; i < maxParticles; i++)
			{
				int offset = i * verticesPerParticle;
				if (useThreeZoneSlicing)
				{
					vertices.AddRange(new Vector3[8]);
					triangles.AddRange(new[] {
						offset+0,offset+1,offset+2, offset+1,offset+3,offset+2,
						offset+2,offset+3,offset+4, offset+3,offset+5,offset+4,
						offset+4,offset+5,offset+6, offset+5,offset+7,offset+6
					});
					colors.AddRange(new Color[8]);
					uvs.AddRange(new[]{ new Vector2(0,0),new Vector2(1,0), new Vector2(0,0.5f),new Vector2(1,0.5f),
										new Vector2(0,0.5f),new Vector2(1,0.5f), new Vector2(0,1),new Vector2(1,1) });
				}
				else
				{
					vertices.AddRange(new Vector3[4]);
					triangles.AddRange(new[] { offset + 0, offset + 1, offset + 2, offset + 1, offset + 3, offset + 2 });
					colors.AddRange(new Color[4]);
					uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.SetColors(colors);
			mesh.SetUVs(0, uvs);
			mesh.RecalculateBounds();
		}

		// NEW: Returns the actual Particle
		public Particle SpawnParticle(Vector3 position, float lifetime, float radius, Color color)
		{
			Particle p = GetInactiveParticle();
			if (p == null) return null;

			p.position = p.previousPosition = position;
			p.lifetime = p.maxLifetime = lifetime;
			p.color = color;
			p.radius = p.initialRadius = radius;

			activeParticles.Add(p);
			activeParticleCount++;
			return p;
		}

		private Particle GetInactiveParticle()
		{
			if (freeParticleIndices.Count == 0) return null;
			int idx = freeParticleIndices[freeParticleIndices.Count - 1];
			freeParticleIndices.RemoveAt(freeParticleIndices.Count - 1);
			return particlePool[idx];
		}

		// NEW: Takes Particle reference
		public void UpdateParticle(Particle particle, Vector3 position, float lifetime, float radius, Color color)
		{
			if (particle == null || particle.lifetime <= 0f) return;

			particle.previousPosition = particle.position;
			particle.position = position;
			particle.lifetime = lifetime;
			particle.radius = radius;
			particle.color = color;

			if (lifetime <= 0f)
			{
				DeactivateQuad(particle.vertexIndex);
				freeParticleIndices.Add(particle.poolIndex);
				activeParticles.Remove(particle);
				activeParticleCount--;
			}
		}

		private void DeactivateQuad(int vertexIndex)
		{
			for (int v = 0; v < verticesPerParticle; v++)
			{
				vertices[vertexIndex + v] = Vector3.zero;
				colors[vertexIndex + v] = Color.clear;
			}
		}

		public void Render()
		{
			UpdateMesh();
			Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0);
		}

		private void UpdateMesh()
		{
			var camPos = mainCamera.transform.position;

			for (int i = 0; i < activeParticleCount; i++)
			{
				if (i >= activeParticles.Count) { activeParticleCount = activeParticles.Count; break; }
				var p = activeParticles[i];
				if (p.lifetime <= 0f) continue;

				var pos = p.position;
				var prev = p.previousPosition;
				var delta = pos - prev;
				var toCam = (pos - camPos).normalized;
				var tangent = Vector3.Cross(toCam, delta).normalized;

				float dot = Vector3.Dot(delta, toCam);
				float tang = (delta - dot * toCam).magnitude;
				if (tang < p.radius)
				{
					var cross = Vector3.Cross(tangent, toCam);
					delta += (p.radius - tang) * cross;
				}

				var head = pos + delta;
				var tail = pos - delta;
				var rad = tangent * p.radius;
				int v = p.vertexIndex;

				if (useThreeZoneSlicing)
				{
					float velComp = tang > 0.0001f ? Mathf.Max(0, tang - p.radius) / tang : 0f;
					var half = velComp * delta;
					var headB = pos + half;
					var tailB = pos - half;

					vertices[v + 0] = head + rad; vertices[v + 1] = head - rad;
					vertices[v + 2] = headB + rad; vertices[v + 3] = headB - rad;
					vertices[v + 4] = tailB + rad; vertices[v + 5] = tailB - rad;
					vertices[v + 6] = tail + rad; vertices[v + 7] = tail - rad;

					for (int j = 0; j < 8; j++) colors[v + j] = p.color;
				}
				else
				{
					vertices[v + 0] = tail - rad; vertices[v + 1] = tail + rad;
					vertices[v + 2] = head - rad; vertices[v + 3] = head + rad;
					for (int j = 0; j < 4; j++) colors[v + j] = p.color;
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.RecalculateBounds();
		}
	}
}