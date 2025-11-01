using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public struct ParticleUpdateContext
	{
		public ParticleController controller;
		public float deltaTime;
		public float normalizedLife;
	}

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

	public class ParticleMesh
	{
		public Mesh mesh;                    // Output mesh
		public Matrix4x4 viewMatrix;         // View matrix used to build it
		public bool usedThisFrame;           // Was this mesh built this frame?
	}

	public class ParticleSystem
	{
		private const int MaxParticles = 4096;
		private const int MaxViewCache = 8;
		private readonly bool useThreeZoneSlicing;
		private readonly Material material;
		private readonly int verticesPerParticle;

		private readonly List<Particle> particlePool = new List<Particle>(MaxParticles);
		public readonly List<Particle> activeParticles = new List<Particle>(MaxParticles);
		private readonly List<int> freeParticleIndices = new List<int>(MaxParticles);

		private readonly List<Vector3> vertices;
		private readonly List<int> triangles;
		private readonly List<Color> colors;
		private readonly List<Vector2> uvs;

		// Fixed-size cache using ParticleMesh — matches original behavior exactly
		private readonly ParticleMesh[] particleMeshes = new ParticleMesh[MaxViewCache];
		private int viewCount = 0;

		// Placeholder for future use
		private readonly List<Vector3> positions;

		public ParticleController Controller { get; private set; }
		private ParticleUpdateContext updateCtx;
		private readonly HashSet<int> dirtyColorIndices = new HashSet<int>();

		public ParticleSystem(Material particleMaterial, bool threeZoneSlicing, ParticleController controller)
		{
			material = new Material(particleMaterial);
			useThreeZoneSlicing = threeZoneSlicing;
			verticesPerParticle = useThreeZoneSlicing ? 8 : 4;
			Controller = controller;

			int trianglesPerParticle = useThreeZoneSlicing ? 18 : 6;
			int totalTriangles = MaxParticles * trianglesPerParticle;

			vertices = new List<Vector3>(MaxParticles * 8);
			triangles = new List<int>(totalTriangles);
			colors = new List<Color>(MaxParticles * 8);
			uvs = new List<Vector2>(MaxParticles * 8);
			positions = new List<Vector3>(MaxParticles); // Kept for future use

			InitializePool();
			InitializeSharedBuffers();
			SetupURPMaterial();

			updateCtx = new ParticleUpdateContext();
		}

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

		private void InitializePool()
		{
			for (int i = 0; i < MaxParticles; i++)
			{
				particlePool.Add(null);
				freeParticleIndices.Add(i);
			}
		}

		private void InitializeSharedBuffers()
		{
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
					uvs.AddRange(new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) });
				}
			}

			// Initialize all cached meshes with shared topology
			for (int i = 0; i < MaxViewCache; i++)
			{
				var mesh = new Mesh { name = $"ParticleViewMesh_{i}" };
				mesh.MarkDynamic();
				mesh.SetVertices(vertices);
				mesh.SetTriangles(triangles, 0);
				mesh.SetColors(colors);
				mesh.SetUVs(0, uvs);
				mesh.RecalculateBounds();

				particleMeshes[i] = new ParticleMesh
				{
					mesh = mesh,
					viewMatrix = Matrix4x4.identity,
					usedThisFrame = false
				};
			}
		}

		public T AllocateParticle<T>() where T : Particle, new()
		{
			if (freeParticleIndices.Count == 0) return null;

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
		// UPDATE: Reset view cache at frame start
		// ----------------------------------------------------------------
		public void UpdateParticles()
		{
			if (Controller == null) return;

			float dt = Time.deltaTime;
			updateCtx.controller = Controller;
			updateCtx.deltaTime = dt;

			// Reset view cache
			viewCount = 0;
			for (int i = 0; i < MaxViewCache; i++)
				particleMeshes[i].usedThisFrame = false;

			dirtyColorIndices.Clear();

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

				int baseIdx = p.vertexIndex;
				if (!colors[baseIdx].Equals(p.color))
					dirtyColorIndices.Add(p.poolIndex);
			}
		}

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
		// RENDER: Matrix-driven, rebuild only if new this frame
		// ----------------------------------------------------------------
		public void Render(Camera renderingCamera)
		{
			if (renderingCamera == null) return;

			Matrix4x4 view = renderingCamera.worldToCameraMatrix;
			int slot = FindOrCreateViewSlot(view);
			ParticleMesh pm = particleMeshes[slot];

			if (!pm.usedThisFrame)
			{
				UpdateMeshInternal(renderingCamera, vertices, colors);

				pm.mesh.SetVertices(vertices);

				if (dirtyColorIndices.Count > 0)
				{
					foreach (int poolIdx in dirtyColorIndices)
					{
						Particle p = particlePool[poolIdx];
						if (p == null) continue;
						int v = p.vertexIndex;
						for (int i = 0; i < verticesPerParticle; i++)
							colors[v + i] = p.color;
					}
					dirtyColorIndices.Clear();
				}

				pm.mesh.SetColors(colors);
				pm.mesh.RecalculateBounds();

				pm.viewMatrix = view;
				pm.usedThisFrame = true;
			}

			Graphics.DrawMesh(pm.mesh, Matrix4x4.identity, material, 0, renderingCamera);
		}

		// ----------------------------------------------------------------
		// FIND OR CREATE SLOT BASED ON MATRIX
		// ----------------------------------------------------------------
		private int FindOrCreateViewSlot(Matrix4x4 view)
		{
			for (int i = 0; i < viewCount; i++)
			{
				if (particleMeshes[i].usedThisFrame && MatricesEqual(view, particleMeshes[i].viewMatrix))
					return i;
			}

			if (viewCount >= MaxViewCache)
			{
				// Reuse slot 0 (original behavior)
				viewCount = 1;
				particleMeshes[0].usedThisFrame = false;
				particleMeshes[0].viewMatrix = view;
				return 0;
			}

			int slot = viewCount++;
			particleMeshes[slot].viewMatrix = view;
			particleMeshes[slot].usedThisFrame = false;
			return slot;
		}

		private bool MatricesEqual(Matrix4x4 a, Matrix4x4 b)
		{
			for (int i = 0; i < 16; i++)
				if (!Mathf.Approximately(a[i], b[i]))
					return false;
			return true;
		}

		// ----------------------------------------------------------------
		// MESH UPDATE (per-view)
		// ----------------------------------------------------------------
		private void UpdateMeshInternal(Camera renderingCamera, List<Vector3> verts, List<Color> cols)
		{
			Matrix4x4 view = renderingCamera.worldToCameraMatrix;
			Matrix4x4 camToWorld = view.inverse;
			Vector3 camPos = camToWorld.MultiplyPoint(Vector3.zero);
			Vector3 camUp = camToWorld.MultiplyVector(Vector3.up).normalized;

			for (int i = 0; i < activeParticles.Count; i++)
			{
				Particle p = activeParticles[i];
				if (p.life <= 0f) continue;

				Vector3 pos = p.position;
				Vector3 delta = p.delta;
				Vector3 toCam = (pos - camPos).normalized;

				Vector3 tangent;
				if (delta.sqrMagnitude > 0.000001f)
					tangent = Vector3.Cross(delta, toCam).normalized;
				else
				{
					tangent = Vector3.Cross(camUp, toCam).normalized;
					if (tangent.sqrMagnitude < 0.01f)
						tangent = Vector3.Cross(Vector3.up, toCam).normalized;
				}

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

					verts[v + 0] = head - rad; verts[v + 1] = head + rad;
					verts[v + 2] = headB - rad; verts[v + 3] = headB + rad;
					verts[v + 4] = tailB - rad; verts[v + 5] = tailB + rad;
					verts[v + 6] = tail - rad; verts[v + 7] = tail + rad;
				}
				else
				{
					verts[v + 0] = head - rad; verts[v + 1] = head + rad;
					verts[v + 2] = tail - rad; verts[v + 3] = tail + rad;
				}
			}
		}

#if UNITY_EDITOR
		// Returns the main camera's mesh (or first valid one) for Scene View
		public Mesh GetDebugMesh()
		{
			for (int i = 0; i < viewCount; i++)
			{
				if (particleMeshes[i].usedThisFrame)
					return particleMeshes[i].mesh;
			}
			return viewCount > 0 ? particleMeshes[0].mesh : null;
		}
#endif
	}
}