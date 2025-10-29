using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class ParticleSystem
	{
		public class ParticleSettings
		{
			public float lifetime = 1f;
			public float radius = 0.01f; // Controls size for simple particles and body radius for three-zone
			public Color color = Color.white;
		}

		private readonly int maxParticles = 4096;
		private readonly bool useThreeZoneSlicing;
		private readonly Material material;
		private Mesh mesh;
		private readonly Camera mainCamera;

		private class Particle
		{
			public int vertexIndex;           // Starting vertex index in mesh
			public int poolIndex;             // Index in particlePool
			public Vector3 position;          // World space
			public Vector3 previousPosition;  // World space
			public float lifetime;            // < 0 → not in use, > 0 → alive
			public float maxLifetime;         // Initial lifetime
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
				Debug.LogWarning($"ParticleSystem: Material shader is {material.shader.name}, expected 'MassiveHadronLtd/Unlit/AdditiveParticles'. Attempting to set shader.");
				Shader additiveShader = Shader.Find("MassiveHadronLtd/Unlit/AdditiveParticles");
				if (additiveShader == null)
				{
					Debug.LogError("ParticleSystem: Could not find 'MassiveHadronLtd/Unlit/AdditiveParticles' shader.");
					return;
				}
				material.shader = additiveShader;
			}

			material.SetColor("_BaseColor", Color.white);
			material.SetFloat("_ZWrite", 0);
			material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

			if (material.GetTexture("_BaseMap") == null)
			{
				Debug.LogWarning("ParticleSystem: No texture assigned to _BaseMap in material. Assign a spark texture for proper rendering.");
			}
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
					// isActive = false → REMOVED
					vertexIndex = i * verticesPerParticle,
					poolIndex = i,
					lifetime = -1f  // Explicit "dead" sentinel
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
				int vertexOffset = i * verticesPerParticle;

				if (useThreeZoneSlicing)
				{
					vertices.AddRange(new Vector3[8]);
					triangles.AddRange(new[]
					{
						vertexOffset + 0, vertexOffset + 1, vertexOffset + 2,
						vertexOffset + 1, vertexOffset + 3, vertexOffset + 2,
						vertexOffset + 2, vertexOffset + 3, vertexOffset + 4,
						vertexOffset + 3, vertexOffset + 5, vertexOffset + 4,
						vertexOffset + 4, vertexOffset + 5, vertexOffset + 6,
						vertexOffset + 5, vertexOffset + 7, vertexOffset + 6
					});
					colors.AddRange(new Color[8]);
					uvs.AddRange(new[]
					{
						new Vector2(0f, 0f), new Vector2(1f, 0f),
						new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
						new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
						new Vector2(0f, 1f), new Vector2(1f, 1f)
					});
				}
				else
				{
					vertices.AddRange(new Vector3[4]);
					triangles.AddRange(new[]
					{
						vertexOffset + 0, vertexOffset + 1, vertexOffset + 2,
						vertexOffset + 1, vertexOffset + 3, vertexOffset + 2
					});
					colors.AddRange(new Color[4]);
					uvs.AddRange(new[]
					{
						new Vector2(0f, 0f), new Vector2(1f, 0f),
						new Vector2(0f, 1f), new Vector2(1f, 1f)
					});
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.SetColors(colors);
			mesh.SetUVs(0, uvs);
			mesh.RecalculateBounds();
		}

		public int SpawnParticle(Vector3 position, ParticleSettings settings)
		{
			Particle particle = GetInactiveParticle();
			if (particle == null) return -1;

			particle.position = position;
			particle.previousPosition = position;
			particle.lifetime = settings.lifetime;
			particle.maxLifetime = settings.lifetime;
			particle.color = settings.color;
			particle.radius = settings.radius;
			particle.initialRadius = settings.radius;
			// particle.isActive = true → REMOVED

			activeParticles.Add(particle);
			activeParticleCount++;
			return particle.poolIndex;
		}

		private Particle GetInactiveParticle()
		{
			if (freeParticleIndices.Count == 0) return null;
			int poolIndex = freeParticleIndices[freeParticleIndices.Count - 1];
			freeParticleIndices.RemoveAt(freeParticleIndices.Count - 1);
			return particlePool[poolIndex];
		}

		public void UpdateParticle(int poolIndex, Vector3 position, float lifetime,
								   float radius, Color color)
		{
			if (poolIndex < 0 || poolIndex >= particlePool.Count)
			{
				Debug.LogWarning($"UpdateParticle: Invalid poolIndex {poolIndex}");
				return;
			}

			Particle particle = particlePool[poolIndex];

			// Ignore updates on dead particles
			if (particle.lifetime <= 0f) return;

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
			// Upload happens in UpdateMesh()
		}

		public void Render()
		{
			UpdateMesh();
			Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0);
		}

		private void UpdateMesh()
		{
			var vCamPos = mainCamera.transform.position;

			for (var i = 0; i < activeParticleCount; i++)
			{
				if (i >= activeParticles.Count)
				{
					Debug.LogWarning($"UpdateMesh: Index {i} out of bounds for activeParticles.Count {activeParticles.Count}, activeParticleCount {activeParticleCount}");
					activeParticleCount = activeParticles.Count;
					break;
				}

				var particle = activeParticles[i];
				if (particle.lifetime <= 0f) continue; // Guard

				var vParticlePos = particle.position;
				var vParticleOld = particle.previousPosition;
				var vParticleDelta = vParticlePos - vParticleOld;
				var vCamParticle = (vParticlePos - vCamPos).normalized;
				var vTanParticle = Vector3.Cross(vCamParticle, vParticleDelta).normalized;

				var dot = Vector3.Dot(vParticleDelta, vCamParticle);
				var tangentialComponent = (vParticleDelta - dot * vCamParticle).magnitude;

				if (tangentialComponent < particle.radius)
				{
					var vCross = Vector3.Cross(vTanParticle, vCamParticle);
					vParticleDelta += (particle.radius - tangentialComponent) * vCross;
				}

				var vertexIndex = particle.vertexIndex;
				var headPos = vParticlePos + vParticleDelta;
				var tailPos = vParticlePos - vParticleDelta;
				var vTangentRadius = vTanParticle * particle.radius;

				if (useThreeZoneSlicing)
				{
					var velocityComponent = tangentialComponent > 0.0001f ? Mathf.Max(0, tangentialComponent - particle.radius) / tangentialComponent : 0f;
					var vHalfBody = velocityComponent * vParticleDelta;
					var headBody = vParticlePos + vHalfBody;
					var tailBody = vParticlePos - vHalfBody;

					vertices[vertexIndex + 0] = headPos + vTangentRadius;
					vertices[vertexIndex + 1] = headPos - vTangentRadius;
					vertices[vertexIndex + 2] = headBody + vTangentRadius;
					vertices[vertexIndex + 3] = headBody - vTangentRadius;
					vertices[vertexIndex + 4] = tailBody + vTangentRadius;
					vertices[vertexIndex + 5] = tailBody - vTangentRadius;
					vertices[vertexIndex + 6] = tailPos + vTangentRadius;
					vertices[vertexIndex + 7] = tailPos - vTangentRadius;

					for (var j = 0; j < 8; j++) colors[vertexIndex + j] = particle.color;
				}
				else
				{
					vertices[vertexIndex + 0] = tailPos - vTangentRadius;
					vertices[vertexIndex + 1] = tailPos + vTangentRadius;
					vertices[vertexIndex + 2] = headPos - vTangentRadius;
					vertices[vertexIndex + 3] = headPos + vTangentRadius;

					for (var j = 0; j < 4; j++) colors[vertexIndex + j] = particle.color;
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.RecalculateBounds();
		}
	}
}