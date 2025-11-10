// File: Assets/System/Scripts/ParticleSystem.cs
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
		public int poolIndex;
		public Vector3 position;
		public Vector3 delta;
		public float radius;
		public Color color;

		internal QuadStrip strip;

		public abstract void Update(ref ParticleUpdateContext ctx);
	}

	public class ParticleMesh
	{
		public Mesh mesh;
		public Matrix4x4 viewMatrix;
	}

	public class ParticleSystem
	{
		private const int MaxParticles = 4096;
		public const int MaxViewCache = 8;
		private readonly bool useThreeZoneSlicing;
		private readonly Material material;

		private readonly QuadStripAllocator _allocator = new();

		private readonly List<Particle> particlePool = new(MaxParticles);
		public readonly List<Particle> activeParticles = new(MaxParticles);
		private readonly Stack<Particle> freeParticles = new();

		private readonly ParticleMesh[] particleMeshes = new ParticleMesh[MaxViewCache];
		private int viewCount = 0;

		private bool coloursUpdatedThisFrame;
		private ParticleUpdateContext updateCtx;

		public int ViewCount => viewCount;
		public int ActiveParticleCount => activeParticles.Count;
		public ParticleController Controller { get; private set; }

		public ParticleSystem(Material particleMaterial, bool threeZoneSlicing, ParticleController controller)
		{
			material = new Material(particleMaterial);
			useThreeZoneSlicing = threeZoneSlicing;
			Controller = controller;

			int quadsPerParticle = useThreeZoneSlicing ? 3 : 1;
			int totalQuads = MaxParticles * quadsPerParticle;
			_allocator.SetMaxIndexBlocks(totalQuads);
			_allocator.SetMaxVertexBlocks(totalQuads + MaxParticles);

			for (int i = 0; i < MaxParticles; i++)
				particlePool.Add(null);

			for (int i = 0; i < MaxViewCache; i++)
			{
				var mesh = new Mesh { name = $"ParticleViewMesh_{i}" };
				mesh.MarkDynamic();
				particleMeshes[i] = new ParticleMesh { mesh = mesh, viewMatrix = Matrix4x4.zero };
			}

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

		public T AllocateParticle<T>() where T : Particle, new()
		{
			Particle p;
			if (freeParticles.Count > 0)
			{
				p = freeParticles.Pop();
				if (p.strip != null)
				{
					_allocator.ReleaseStrip(p.strip);
					p.strip = null;
				}
			}
			else
			{
				p = new T();
				int poolIdx = particlePool.Count;
				while (poolIdx >= particlePool.Count) particlePool.Add(null);
				p.poolIndex = poolIdx;
			}

			p.life = 1f;
			p.duration = 1f;
			p.initialRadius = 0.1f;
			p.radius = 0.1f;
			p.position = Vector3.zero;
			p.delta = Vector3.zero;
			p.color = Color.white;

			int quads = useThreeZoneSlicing ? 3 : 1;
			p.strip = _allocator.AllocateStrip(quads);

			// WRITE UVs ONCE, HERE
			WriteUVsForStrip(p.strip, quads);

			particlePool[p.poolIndex] = p;
			activeParticles.Add(p);
			return (T)p;
		}

		private void WriteUVsForStrip(QuadStrip strip, int quads)
		{
			var uvs = _allocator.MutableUV;
			var blocks = strip.vertexBlocks;

			if (quads == 3)
			{
				int v0 = blocks[0] * 2;
				int v1 = blocks[1] * 2;
				int v2 = blocks[2] * 2;
				int v3 = blocks[3] * 2;

				uvs[v0 + 0] = new Vector2(0, 1); uvs[v0 + 1] = new Vector2(1, 1);
				uvs[v1 + 0] = new Vector2(0, 0.5f); uvs[v1 + 1] = new Vector2(1, 0.5f);
				uvs[v2 + 0] = new Vector2(0, 0.5f); uvs[v2 + 1] = new Vector2(1, 0.5f);
				uvs[v3 + 0] = new Vector2(0, 0); uvs[v3 + 1] = new Vector2(1, 0);
			}
			else // quads == 1
			{
				int v0 = blocks[0] * 2;
				int v1 = blocks[1] * 2;

				uvs[v0 + 0] = new Vector2(0, 1); uvs[v0 + 1] = new Vector2(1, 1);
				uvs[v1 + 0] = new Vector2(0, 0); uvs[v1 + 1] = new Vector2(1, 0);
			}
		}

		public void UpdateParticles()
		{
			if (Controller == null) return;

			float dt = Time.deltaTime;
			updateCtx.controller = Controller;
			updateCtx.deltaTime = dt;

			viewCount = 0;
			for (int i = 0; i < MaxViewCache; i++)
				particleMeshes[i].viewMatrix = Matrix4x4.zero;

			coloursUpdatedThisFrame = false;

			for (int i = activeParticles.Count - 1; i >= 0; i--)
			{
				var p = activeParticles[i];
				if (p.life <= 0f) continue;

				p.life -= dt;
				if (p.life <= 0f)
				{
					if (p.strip != null)
					{
						_allocator.ReleaseStrip(p.strip);
						p.strip = null;
					}
					freeParticles.Push(p);
					activeParticles.RemoveAt(i);
					continue;
				}

				updateCtx.normalizedLife = 1f - (p.life / p.duration);
				p.Update(ref updateCtx);
			}
		}

		public void Render(Camera renderingCamera)
		{
			if (renderingCamera == null || activeParticles.Count == 0) return;

			if (!coloursUpdatedThisFrame)
			{
				UpdateColors();
				coloursUpdatedThisFrame = true;
			}

			Matrix4x4 view = renderingCamera.worldToCameraMatrix;
			int slot = FindMatchingViewSlot(view);
			if (slot == -1)
			{
				slot = CreateViewSlot(view);
				UpdateMeshForView(renderingCamera, slot);
			}

			ReportViewRendered(view);
			Graphics.DrawMesh(particleMeshes[slot].mesh, Matrix4x4.identity, material, 0, renderingCamera);
		}

		private void UpdateColors()
		{
			var colors = _allocator.MutableColors;
			foreach (var p in activeParticles)
			{
				if (p.life <= 0f || p.strip == null) continue;
				Color c = p.color;
				var blocks = p.strip.vertexBlocks;
				for (int i = 0; i < blocks.Count; i++)
				{
					int v = blocks[i] * 2;
					if (v + 1 < colors.Count)
					{
						colors[v + 0] = c;
						colors[v + 1] = c;
					}
				}
			}
		}

		private void UpdateMeshForView(Camera cam, int slot)
		{
			var verts = _allocator.MutableVertices;

			Matrix4x4 camToWorld = cam.worldToCameraMatrix.inverse;
			Vector3 camPos = camToWorld.MultiplyPoint(Vector3.zero);
			Vector3 camUp = camToWorld.MultiplyVector(Vector3.up).normalized;

			foreach (var p in activeParticles)
			{
				if (p.life <= 0f || p.strip == null) continue;

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

				var blocks = p.strip.vertexBlocks;
				int quads = p.strip.indexBlocks.Count;

				if (useThreeZoneSlicing && quads == 3)
				{
					float velComp = tang > 0.0001f ? Mathf.Max(0, tang - p.radius) / tang : 0f;
					Vector3 half = velComp * delta;
					Vector3 headB = pos + half;
					Vector3 tailB = pos - half;

					WriteSegment(verts, blocks, 0, head, headB, rad);
					WriteSegment(verts, blocks, 1, headB, tailB, rad);
					WriteSegment(verts, blocks, 2, tailB, tail, rad);
				}
				else if (quads == 1)
				{
					WriteSegment(verts, blocks, 0, head, tail, rad);
				}
			}

			var mesh = particleMeshes[slot].mesh;
			mesh.Clear();
			mesh.SetVertices(verts);
			mesh.SetColors(_allocator.MutableColors);
			mesh.SetUVs(0, _allocator.MutableUV);  // UVs are correct from allocation
			mesh.SetIndices(_allocator.MutableIndices, MeshTopology.Triangles, 0);
			mesh.RecalculateBounds();
		}

		private void WriteSegment(List<Vector3> verts, List<int> blocks, int segIndex, Vector3 a, Vector3 b, Vector3 rad)
		{
			int v0 = blocks[segIndex] * 2;
			int v1 = blocks[segIndex + 1] * 2;

			if (v1 + 1 >= verts.Count) return;

			verts[v0 + 0] = a - rad;
			verts[v0 + 1] = a + rad;
			verts[v1 + 0] = b - rad;
			verts[v1 + 1] = b + rad;
		}

		private int FindMatchingViewSlot(Matrix4x4 view)
		{
			for (int i = 0; i < viewCount; i++)
			{
				if (particleMeshes[i].viewMatrix != Matrix4x4.zero && MatricesEqual(view, particleMeshes[i].viewMatrix))
					return i;
			}
			return -1;
		}

		private int CreateViewSlot(Matrix4x4 view)
		{
			if (viewCount >= MaxViewCache)
			{
				viewCount = 1;
				particleMeshes[0].viewMatrix = view;
				return 0;
			}
			int slot = viewCount++;
			particleMeshes[slot].viewMatrix = view;
			return slot;
		}

		private bool MatricesEqual(Matrix4x4 a, Matrix4x4 b)
		{
			const float epsilon = 1e-6f;
			for (int i = 0; i < 16; i++)
				if (Mathf.Abs(a[i] - b[i]) > epsilon)
					return false;
			return true;
		}

		private static readonly HashSet<int> _renderedViewHashes = new();
		private static int _lastFrame = -1;

		public static int GlobalSlotsUsed => Time.frameCount != _lastFrame ? 0 : _renderedViewHashes.Count;

		public static void ResetGlobalTracking()
		{
			_renderedViewHashes.Clear();
			_lastFrame = Time.frameCount;
		}

		public static void ReportViewRendered(Matrix4x4 viewMatrix)
		{
			if (Time.frameCount != _lastFrame) ResetGlobalTracking();
			int hash = GetMatrixHash(viewMatrix);
			_renderedViewHashes.Add(hash);
		}

		private static int GetMatrixHash(Matrix4x4 m)
		{
			unchecked
			{
				int hash = 17;
				for (int i = 0; i < 16; i++)
					hash = hash * 31 + m[i].GetHashCode();
				return hash;
			}
		}

		public void Defrag() => _allocator.Defrag();

		public void Clear()
		{
			foreach (var p in activeParticles)
				if (p.strip != null) _allocator.ReleaseStrip(p.strip);
			activeParticles.Clear();
			freeParticles.Clear();
			_allocator.Defrag();
		}

#if UNITY_EDITOR
		public Mesh GetDebugMesh()
		{
			for (int i = 0; i < viewCount; i++)
			{
				if (particleMeshes[i].viewMatrix != Matrix4x4.zero)
					return particleMeshes[i].mesh;
			}
			return viewCount > 0 ? particleMeshes[0].mesh : null;
		}
#endif
	}
}