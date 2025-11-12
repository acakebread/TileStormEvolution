using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class Particle
	{
		public int poolIndex;
		public int vertexIndex;
		public float life;
		public float duration;
		
		public Vector3 position;
		public Vector3 oldPosition;
		public Vector3 delta => position - oldPosition;

		public float radius;
		public Color color;

		public List<ParticleBehaviour> behaviours = new();

		public virtual void Initialize() { foreach (var b in behaviours) b.Initialize(this); }

		public virtual void Update(float deltaTime = 0f) { foreach (var b in behaviours) b.Update(this, deltaTime); }

		public T GetBehaviour<T>() where T : ParticleBehaviour
		{
			foreach (var b in behaviours)
				if (b is T tb) return tb;
			return null;
		}
	}

	public abstract class ParticleSystem
	{
		protected const int MaxParticles = 4096;
		public const int MaxViewCache = 8;

		protected readonly Material material;

		protected readonly List<Particle> particlePool = new();
		protected readonly List<Particle> activeParticles = new();
		protected readonly List<int> freeParticleIndices = new();

		protected List<Vector3> vertices;
		protected List<int> triangles;
		protected List<Color> colors;
		protected List<Vector2> uvs;

		protected bool coloursUpdatedThisFrame;

		private class ParticleMesh
		{
			public Mesh mesh;
			public Matrix4x4 viewMatrix;
		}

		private readonly ParticleMesh[] particleMeshes = new ParticleMesh[MaxViewCache];
		private int viewCount = 0;

		public int ViewCount => viewCount;
		public int ActiveParticleCount => activeParticles.Count;

		public ParticleController Controller { get; protected set; }

		protected ParticleSystem(Material particleMaterial, ParticleController controller)
		{
			material = new Material(particleMaterial);
			Controller = controller;

			Initialize();
			SetupURPMaterial();
			CreateViewMeshes();
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

		protected virtual void Initialize()
		{
			for (int i = 0; i < MaxParticles; i++)
			{
				particlePool.Add(null);
				freeParticleIndices.Add(i);
			}
		}

		protected abstract void InitializeBuffers();

		private void CreateViewMeshes()
		{
			for (int i = 0; i < MaxViewCache; i++)
			{
				var mesh = new Mesh { name = $"ParticleMesh_{i}" };
				mesh.MarkDynamic();
				mesh.SetVertices(vertices);
				mesh.SetTriangles(triangles, 0);
				mesh.SetColors(colors);
				mesh.SetUVs(0, uvs);
				mesh.RecalculateBounds();

				particleMeshes[i] = new ParticleMesh
				{
					mesh = mesh,
					viewMatrix = Matrix4x4.zero
				};
			}
		}

		public abstract Particle AllocateParticle();

		public virtual void UpdateParticles(float deltaTime = 0f)//0f for initialise
		{
			if (Controller == null) return;

			float dt = Time.deltaTime;

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
					DeactivateParticle(p, i);
					continue;
				}
				p.Update(deltaTime);
			}
		}

		protected virtual void DeactivateParticle(Particle p, int activeIndex)
		{
			freeParticleIndices.Add(p.poolIndex);
			activeParticles.RemoveAt(activeIndex);
		}

		public virtual void Render(Camera renderingCamera)
		{
			if (renderingCamera == null) return;

			if (!coloursUpdatedThisFrame)
			{
				UpdateColors();
				coloursUpdatedThisFrame = true;
			}

			ParticleMesh pm = null;
			Matrix4x4 view = renderingCamera.worldToCameraMatrix;
			int slot = FindMatchingViewSlot(view);
			if (slot == -1)
			{
				slot = CreateViewSlot(view);
				pm = particleMeshes[slot];
				UpdateMesh(renderingCamera, vertices, colors);
				pm.mesh.SetVertices(vertices);
				pm.mesh.SetColors(colors);
				pm.mesh.RecalculateBounds();
				pm.viewMatrix = view;
			}
			ReportViewRendered(view);
			pm = particleMeshes[slot];
			Graphics.DrawMesh(pm.mesh, Matrix4x4.identity, material, 0, renderingCamera);
		}

		protected virtual void UpdateColors() { }

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

		protected abstract void UpdateMesh(Camera renderingCamera, List<Vector3> verts, List<Color> cols);

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