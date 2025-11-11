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
		public Mesh mesh;
		public Matrix4x4 viewMatrix;
	}

	public abstract class ParticleSystem
	{
		protected const int MaxParticles = 4096;
		public const int MaxViewCache = 8;

		protected readonly Material material;

		protected readonly List<Particle> particlePool = new();
		public readonly List<Particle> activeParticles = new();
		protected readonly List<int> freeParticleIndices = new();

		// **Buffers are now abstract – derived classes own them**
		protected List<Vector3> vertices;
		protected List<int> triangles;
		protected List<Color> colors;
		protected List<Vector2> uvs;

		protected bool coloursUpdatedThisFrame;
		protected readonly ParticleMesh[] particleMeshes = new ParticleMesh[MaxViewCache];
		protected int viewCount = 0;

		public int ViewCount => viewCount;
		public int ActiveParticleCount => activeParticles.Count;

		public ParticleController Controller { get; protected set; }
		protected ParticleUpdateContext updateCtx;

		protected ParticleSystem(Material particleMaterial, ParticleController controller)
		{
			material = new Material(particleMaterial);
			Controller = controller;

			InitializePool();
			PostInitialize();                 // <-- derived classes allocate buffers here
			SetupURPMaterial();

			updateCtx = new ParticleUpdateContext();
		}

		protected virtual void SetupURPMaterial()
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

		protected virtual void InitializePool()
		{
			for (int i = 0; i < MaxParticles; i++)
			{
				particlePool.Add(null);
				freeParticleIndices.Add(i);
			}
		}

		// **Derived classes allocate and fill buffers here**
		protected abstract void PostInitialize();

		public T AllocateParticle<T>() where T : Particle, new()
		{
			if (freeParticleIndices.Count == 0) return null;

			int idx = freeParticleIndices[freeParticleIndices.Count - 1];
			freeParticleIndices.RemoveAt(freeParticleIndices.Count - 1);

			var p = new T
			{
				vertexIndex = idx * GetVerticesPerParticle(),
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

		protected abstract int GetVerticesPerParticle();

		public virtual void UpdateParticles()
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

		protected virtual void DeactivateParticle(int index)
		{
			Particle p = activeParticles[index];
			int v = p.vertexIndex;
			int vpc = GetVerticesPerParticle();
			for (int i = 0; i < vpc; i++)
			{
				vertices[v + i] = Vector3.zero;
				colors[v + i] = Color.clear;
			}
			freeParticleIndices.Add(p.poolIndex);
			activeParticles.RemoveAt(index);
		}

		public virtual void Render(Camera renderingCamera)
		{
			if (renderingCamera == null) return;

			if (!coloursUpdatedThisFrame)
			{
				int vpc = GetVerticesPerParticle();
				for (int i = 0; i < activeParticles.Count; i++)
				{
					Particle p = activeParticles[i];
					if (p.life <= 0f) continue;

					int v = p.vertexIndex;
					for (int j = 0; j < vpc; j++)
						colors[v + j] = p.color;
				}
				coloursUpdatedThisFrame = true;
			}

			ParticleMesh pm = null;
			Matrix4x4 view = renderingCamera.worldToCameraMatrix;
			int slot = FindMatchingViewSlot(view);
			if (slot == -1)
			{
				slot = CreateViewSlot(view);
				pm = particleMeshes[slot];
				UpdateMeshInternal(renderingCamera, vertices, colors);
				pm.mesh.SetVertices(vertices);
				pm.mesh.SetColors(colors);
				pm.mesh.RecalculateBounds();
				pm.viewMatrix = view;
			}
			ReportViewRendered(view);
			pm = particleMeshes[slot];
			Graphics.DrawMesh(pm.mesh, Matrix4x4.identity, material, 0, renderingCamera);
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
			{
				if (Mathf.Abs(a[i] - b[i]) > epsilon)
					return false;
			}
			return true;
		}

		protected abstract void UpdateMeshInternal(Camera renderingCamera, List<Vector3> verts, List<Color> cols);

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

		// GLOBAL TRACKING
		private static readonly HashSet<int> _renderedViewHashes = new();
		private static int _lastFrame = -1;

		public static int GlobalSlotsUsed
		{
			get
			{
				if (Time.frameCount != _lastFrame)
					return 0;
				return _renderedViewHashes.Count;
			}
		}

		public static void ResetGlobalTracking()
		{
			_renderedViewHashes.Clear();
			_lastFrame = Time.frameCount;
		}

		public static void ReportViewRendered(Matrix4x4 viewMatrix)
		{
			if (Time.frameCount != _lastFrame)
				ResetGlobalTracking();

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
	}
}