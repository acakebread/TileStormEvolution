using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class ParticleSystem
	{
		private readonly int maxParticles = 1024;
		private readonly bool useThreeZoneSlicing;
		private Mesh mesh;
		private readonly Material material;
		private readonly Camera mainCamera;

		public abstract class ParticleDataRoot { }

		public class Particle
		{
			public ParticleDataRoot particleData;
			public float life; // < 0 → dead
			public int vertexIndex;
			public int poolIndex;
			public Vector3 position;
			public Vector3 delta;
			public float radius;
			public Color color;
		}

		private List<Particle> particlePool;
		public List<Particle> activeParticles;
		private List<int> freeParticleIndices;
		private List<Vector3> vertices;
		private List<int> triangles;
		private List<Color> colors;
		private List<Vector2> uvs;
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

			for (var i = 0; i < maxParticles; i++)
			{
				var p = new Particle
				{
					vertexIndex = i * verticesPerParticle,
					poolIndex = i,
					life = -1f
				};
				particlePool.Add(p);
				freeParticleIndices.Add(i);
			}
		}

		private void InitializeMesh()
		{
			mesh = new Mesh { name = "ParticleMesh" };
			mesh.MarkDynamic();

			vertices = new List<Vector3>(maxParticles * verticesPerParticle);
			triangles = new List<int>(maxParticles * trianglesPerParticle);
			colors = new List<Color>(maxParticles * verticesPerParticle);
			uvs = new List<Vector2>(maxParticles * verticesPerParticle);

			for (var i = 0; i < maxParticles; i++)
			{
				var offset = i * verticesPerParticle;
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

		public Particle AllocateParticle()
		{
			if (0 == freeParticleIndices.Count) return null;
			var idx = freeParticleIndices[freeParticleIndices.Count - 1];
			freeParticleIndices.RemoveAt(freeParticleIndices.Count - 1);
			activeParticles.Add(particlePool[idx]);
			return particlePool[idx];
		}

		public bool UpdateParticle(Particle particle, Vector3 position, float radius, Color color)
		{
			if (null == particle || particle.life <= 0f)
			{
				Debug.LogError($"updating null particle or praticle with less than zero life!! {particle?.life}");
				return false;
			}

			particle.life -= Time.deltaTime;
			if (particle.life <= 0f)
			{
				//DeactivateQuad
				var vertexIndex = particle.vertexIndex;
				for (var v = 0; v < verticesPerParticle; v++)
				{
					vertices[vertexIndex + v] = Vector3.zero;
					colors[vertexIndex + v] = Color.clear;// don't know if this is necessary
				}
				freeParticleIndices.Add(particle.poolIndex);
				activeParticles.Remove(particle);
				return false;
			}

			particle.delta = position - particle.position;
			particle.position = position;
			particle.radius = radius;
			particle.color = color;
			return true;
		}

		public void Render()
		{
			UpdateMesh();
			Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0);
		}

		private void UpdateMesh()
		{
			var camPos = mainCamera.transform.position;

			for (int i = 0; i < activeParticles.Count; i++)
			{
				var p = activeParticles[i];
				if (p.life <= 0f)
				{
					Debug.LogError("processing inactive particle!!!");
					continue;
				}

				var pos = p.position;
				var delta = p.delta;
				var toCam = (pos - camPos).normalized;
				var tangent = Vector3.Cross(toCam, delta).normalized;

				var dot = Vector3.Dot(delta, toCam);
				var tang = (delta - dot * toCam).magnitude;
				if (tang < p.radius)
				{
					var cross = Vector3.Cross(tangent, toCam);
					delta += (p.radius - tang) * cross;
				}

				var head = pos + delta;
				var tail = pos - delta;
				var rad = tangent * p.radius;
				var v = p.vertexIndex;

				if (useThreeZoneSlicing)
				{
					var velComp = tang > 0.0001f ? Mathf.Max(0, tang - p.radius) / tang : 0f;
					var half = velComp * delta;
					var headB = pos + half;
					var tailB = pos - half;

					vertices[v + 0] = head + rad; vertices[v + 1] = head - rad;
					vertices[v + 2] = headB + rad; vertices[v + 3] = headB - rad;
					vertices[v + 4] = tailB + rad; vertices[v + 5] = tailB - rad;
					vertices[v + 6] = tail + rad; vertices[v + 7] = tail - rad;

					for (var j = 0; j < 8; j++) colors[v + j] = p.color;
				}
				else
				{
					vertices[v + 0] = tail - rad; vertices[v + 1] = tail + rad;
					vertices[v + 2] = head - rad; vertices[v + 3] = head + rad;
					for (var j = 0; j < 4; j++) colors[v + j] = p.color;
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.RecalculateBounds();
		}
	}
}