using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class ParticleSystem
	{
		public class ParticleSettings
		{
			public float width = 0.02f; // Controls size for simple particles and body width for three-zone
			public float lifetime = 1f;
			public bool decay = true; // Shrink width with age, handled by SparkController
			public Color color = Color.white;
		}

		public ParticleSettings defaultSettings = new ParticleSettings();
		private readonly int maxParticles = 4096;
		private readonly bool useThreeZoneSlicing;
		private readonly bool useAdditiveBlending;
		private readonly Material material;
		private Mesh mesh;
		private readonly Camera mainCamera;

		private class Particle
		{
			public Vector3 position; // World space
			public Vector3 previousPosition; // World space
			public Vector3 velocity; // World space, set by SparkController
			public float lifetime; // Current lifetime, set by SparkController
			public float maxLifetime; // Initial lifetime
			public Color color; // Current color, set by SparkController
			public float width; // Current width, set by SparkController
			public float initialWidth; // Initial width
			public float tipSize; // Current tip size for three-zone, set by SparkController
			public bool isActive;
			public int vertexIndex; // Starting vertex index in mesh
			public int poolIndex; // Index in particlePool
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

		public ParticleSystem(Material particleMaterial, bool threeZoneSlicing = false, bool useAdditiveBlending = true)
		{
			material = new Material(particleMaterial);
			useThreeZoneSlicing = threeZoneSlicing;
			this.useAdditiveBlending = useAdditiveBlending;
			verticesPerParticle = useThreeZoneSlicing ? 8 : 4;
			trianglesPerParticle = useThreeZoneSlicing ? 18 : 6;
			mainCamera = Camera.main;

			InitializePool();
			InitializeMesh();
			SetupURPMaterial();
		}

		private void SetupURPMaterial()
		{
			// Ensure the material uses the custom AdditiveParticles shader
			if (material.shader.name != "MassiveHadronLtd/Unlit/AdditiveParticles")
			{
				Debug.LogWarning($"ParticleSystem: Material shader is {material.shader.name}, expected 'MassiveHadronLtd/Unlit/AdditiveParticles'. Attempting to set shader.");
				Shader additiveShader = Shader.Find("MassiveHadronLtd/Unlit/AdditiveParticles");
				if (additiveShader == null)
				{
					Debug.LogError("ParticleSystem: Could not find 'MassiveHadronLtd/Unlit/AdditiveParticles' shader. Please ensure the shader is included in the project.");
					return;
				}
				material.shader = additiveShader;
			}

			// Configure material properties
			material.SetColor("_BaseColor", Color.white); // Base color for vertex color modulation
			material.SetFloat("_ZWrite", 0); // Ensure ZWrite is off
			material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off); // Disable culling
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100; // Slightly higher to render after other transparent objects

			// Check if a texture is assigned
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
				particlePool.Add(new Particle { isActive = false, vertexIndex = i * verticesPerParticle, poolIndex = i });
				freeParticleIndices.Add(i);
			}
			activeParticleCount = 0;
		}

		private void InitializeMesh()
		{
			mesh = new Mesh { name = "ParticleMesh" };
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
					triangles.AddRange(new[] {
						vertexOffset + 0, vertexOffset + 1, vertexOffset + 2, // Tail quad
                        vertexOffset + 1, vertexOffset + 3, vertexOffset + 2,
						vertexOffset + 2, vertexOffset + 3, vertexOffset + 4, // Body quad
                        vertexOffset + 3, vertexOffset + 5, vertexOffset + 4,
						vertexOffset + 4, vertexOffset + 5, vertexOffset + 6, // Head quad
                        vertexOffset + 5, vertexOffset + 7, vertexOffset + 6
					});
					colors.AddRange(new Color[8]);
					uvs.AddRange(new[] {
						new Vector2(0f, 0f), // v0: Tail bottom-left
                        new Vector2(1f, 0f), // v1: Tail bottom-right
                        new Vector2(0f, 0.5f), // v2: Tail top-left
                        new Vector2(1f, 0.5f), // v3: Tail top-right
                        new Vector2(0f, 0.5f), // v4: Body start-left
                        new Vector2(1f, 0.5f), // v5: Body start-right
                        new Vector2(0f, 1f), // v6: Head start-left
                        new Vector2(1f, 1f) // v7: Head start-right
                    });
				}
				else
				{
					vertices.AddRange(new Vector3[4]);
					triangles.AddRange(new[] {
						vertexOffset + 0, vertexOffset + 1, vertexOffset + 2,
						vertexOffset + 1, vertexOffset + 3, vertexOffset + 2
					});
					colors.AddRange(new Color[4]);
					uvs.AddRange(new[] {
						new Vector2(0f, 0f), // v0
                        new Vector2(1f, 0f), // v1
                        new Vector2(0f, 1f), // v2
                        new Vector2(1f, 1f) // v3
                    });
				}
			}
			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.SetColors(colors);
			mesh.SetUVs(0, uvs);
			mesh.RecalculateBounds();
		}

		public int SpawnParticle(Vector3 position, Vector3 velocity, ParticleSettings settings = null)
		{
			if (settings == null) settings = defaultSettings;

			Particle particle = GetInactiveParticle();
			if (particle == null) return -1;

			particle.position = position;
			particle.previousPosition = particle.position;
			particle.velocity = velocity;
			particle.lifetime = settings.lifetime;
			particle.maxLifetime = settings.lifetime;
			particle.color = settings.color;
			particle.width = settings.width;
			particle.initialWidth = settings.width;
			particle.tipSize = settings.width / 2f;
			particle.isActive = true;

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

		public void UpdateParticle(int poolIndex, Vector3 position, Vector3 velocity, float lifetime, float width, float tipSize, Color color)
		{
			if (poolIndex < 0 || poolIndex >= particlePool.Count)
			{
				Debug.LogWarning($"UpdateParticle: Invalid poolIndex {poolIndex}");
				return;
			}

			Particle particle = particlePool[poolIndex];
			if (!particle.isActive) return;

			particle.previousPosition = particle.position;
			particle.position = position;
			particle.velocity = velocity;
			particle.lifetime = lifetime;
			particle.width = width;
			particle.tipSize = tipSize;
			particle.color = color;

			if (particle.lifetime <= 0f)
			{
				particle.isActive = false;
				DeactivateQuad(particle.vertexIndex);
				freeParticleIndices.Add(particle.poolIndex);
				activeParticles.Remove(particle);
				activeParticleCount--;
			}
		}

		private void DeactivateQuad(int vertexIndex)
		{
			int indexOffset = (vertexIndex / verticesPerParticle) * trianglesPerParticle;
			if (useThreeZoneSlicing)
			{
				for (int i = 0; i < 18; i++)
					triangles[indexOffset + i] = vertexIndex;
				for (int i = 0; i < 8; i++)
					colors[vertexIndex + i] = useAdditiveBlending ? Color.black : Color.clear;
			}
			else
			{
				for (int i = 0; i < 6; i++)
					triangles[indexOffset + i] = vertexIndex;
				for (int i = 0; i < 4; i++)
					colors[vertexIndex + i] = useAdditiveBlending ? Color.black : Color.clear;
			}
		}

		public void Render()
		{
			UpdateMesh();
			Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0);
		}

		private void UpdateMesh()
		{
			Vector3 camPos = mainCamera.transform.position;
			Vector3 camRight = mainCamera.transform.right;
			Vector3 camUp = mainCamera.transform.up;

			for (int i = 0; i < activeParticleCount; i++)
			{
				if (i >= activeParticles.Count)
				{
					Debug.LogWarning($"UpdateMesh: Index {i} out of bounds for activeParticles.Count {activeParticles.Count}, activeParticleCount {activeParticleCount}");
					activeParticleCount = activeParticles.Count;
					break;
				}

				Particle particle = activeParticles[i];
				if (!particle.isActive) continue;

				Vector3 pos = particle.position;
				Vector3 prevPos = particle.previousPosition;

				Vector3 delta = pos - prevPos;
				float deltaLength = delta.magnitude;
				Vector3 particleDir = deltaLength > 0.0001f ? delta.normalized : particle.velocity.normalized;

				Vector3 sz = pos - camPos;
				Vector3 sy = Vector3.Cross(sz, particleDir);
				Vector3 sx = camRight;
				if (sy.sqrMagnitude > 0.0001f)
				{
					sy = sy.normalized;
					sx = Vector3.Cross(sy, sz).normalized;
				}
				else
				{
					sy = camUp;
				}
				Vector3 vecy = sy * particle.width * 0.5f;

				int vertexIndex = particle.vertexIndex;
				if (useThreeZoneSlicing)
				{
					Vector3 centerPos = (pos + prevPos) * 0.5f;
					float totalLength = Mathf.Max(particle.width, deltaLength);
					float tipSize = particle.tipSize;
					float bodyLength = totalLength - 2 * tipSize;
					if (bodyLength < 0) bodyLength = 0;

					Vector3 tailTail = centerPos - particleDir * (bodyLength / 2 + tipSize);
					Vector3 tailFront = centerPos - particleDir * (bodyLength / 2);
					vertices[vertexIndex + 0] = tailTail - vecy;
					vertices[vertexIndex + 1] = tailTail + vecy;
					vertices[vertexIndex + 2] = tailFront - vecy;
					vertices[vertexIndex + 3] = tailFront + vecy;

					vertices[vertexIndex + 2] = vertices[vertexIndex + 2];
					vertices[vertexIndex + 3] = vertices[vertexIndex + 3];
					vertices[vertexIndex + 4] = centerPos + particleDir * (bodyLength / 2) - vecy;
					vertices[vertexIndex + 5] = centerPos + particleDir * (bodyLength / 2) + vecy;

					Vector3 headTail = centerPos + particleDir * (bodyLength / 2);
					Vector3 headFront = centerPos + particleDir * (bodyLength / 2 + tipSize);
					vertices[vertexIndex + 4] = vertices[vertexIndex + 4];
					vertices[vertexIndex + 5] = vertices[vertexIndex + 5];
					vertices[vertexIndex + 6] = headFront - vecy;
					vertices[vertexIndex + 7] = headFront + vecy;

					int indexOffset = (vertexIndex / 8) * 18;
					triangles[indexOffset + 0] = vertexIndex + 0;
					triangles[indexOffset + 1] = vertexIndex + 1;
					triangles[indexOffset + 2] = vertexIndex + 2;
					triangles[indexOffset + 3] = vertexIndex + 1;
					triangles[indexOffset + 4] = vertexIndex + 3;
					triangles[indexOffset + 5] = vertexIndex + 2;
					triangles[indexOffset + 6] = vertexIndex + 2;
					triangles[indexOffset + 7] = vertexIndex + 3;
					triangles[indexOffset + 8] = vertexIndex + 4;
					triangles[indexOffset + 9] = vertexIndex + 3;
					triangles[indexOffset + 10] = vertexIndex + 5;
					triangles[indexOffset + 11] = vertexIndex + 4;
					triangles[indexOffset + 12] = vertexIndex + 4;
					triangles[indexOffset + 13] = vertexIndex + 5;
					triangles[indexOffset + 14] = vertexIndex + 6;
					triangles[indexOffset + 15] = vertexIndex + 5;
					triangles[indexOffset + 16] = vertexIndex + 7;
					triangles[indexOffset + 17] = vertexIndex + 6;

					for (int j = 0; j < 8; j++)
						colors[vertexIndex + j] = particle.color;
				}
				else
				{
					Vector3 centerPos = (pos + prevPos) * 0.5f;
					float halfLength = Mathf.Max(particle.width, deltaLength) * 0.5f;
					Vector3 tailPos = centerPos - particleDir * halfLength;
					Vector3 headPos = centerPos + particleDir * halfLength;

					vertices[vertexIndex + 0] = tailPos - vecy;
					vertices[vertexIndex + 1] = tailPos + vecy;
					vertices[vertexIndex + 2] = headPos - vecy;
					vertices[vertexIndex + 3] = headPos + vecy;

					int indexOffset = (vertexIndex / 4) * 6;
					triangles[indexOffset + 0] = vertexIndex + 0;
					triangles[indexOffset + 1] = vertexIndex + 1;
					triangles[indexOffset + 2] = vertexIndex + 2;
					triangles[indexOffset + 3] = vertexIndex + 1;
					triangles[indexOffset + 4] = vertexIndex + 3;
					triangles[indexOffset + 5] = vertexIndex + 2;

					for (int j = 0; j < 4; j++)
						colors[vertexIndex + j] = particle.color;
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.SetColors(colors);
			mesh.RecalculateBounds();
		}
	}
}