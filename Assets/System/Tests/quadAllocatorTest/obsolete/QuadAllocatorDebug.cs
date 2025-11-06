using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class QuadAllocatorDebug : MonoBehaviour
{
	[SerializeField] private QuadAllocator allocator;

	[Header("Particles")]
	public int quadsPerParticle = 4;
	public int maxParticles = 100;
	public float lifetime = 3f;
	public float speed = 1.5f;
	public float spawnRate = 0.2f;

	private Mesh mesh;
	private float spawnTimer;

	private struct Particle
	{
		public float life;
		public float maxLife;
		public Vector2Int pos;
		public Vector2 velocity;
		public int size;
	}

	private readonly List<Particle> particles = new List<Particle>();

	private void Awake()
	{
		var filter = GetComponent<MeshFilter>();
		mesh = new Mesh { name = "DebugMesh" };
		mesh.MarkDynamic();
		filter.sharedMesh = mesh;

		var mat = new Material(Shader.Find("Unlit/Color"));
		mat.color = Color.white;
		GetComponent<MeshRenderer>().sharedMaterial = mat;
	}

	private void Start()
	{
		if (!allocator)
		{
			Debug.LogError("QuadAllocatorDebug: Allocator not assigned!");
			enabled = false;
		}
	}

	private void Update()
	{
		if (!allocator) return;

		// Spawn
		spawnTimer += Time.deltaTime;
		if (spawnTimer >= spawnRate && particles.Count < maxParticles)
		{
			spawnTimer = 0f;
			TrySpawnParticle();
		}

		// Update
		for (int i = particles.Count - 1; i >= 0; i--)
		{
			var p = particles[i];
			p.life -= Time.deltaTime;

			if (p.life <= 0f)
			{
				FreeParticle(p);
				particles.RemoveAt(i);
				continue;
			}

			p.pos.x = Mathf.Clamp(p.pos.x + (int)(p.velocity.x * speed * Time.deltaTime), 0, QuadAllocator.GridSize - p.size);
			p.pos.y = Mathf.Clamp(p.pos.y + (int)(p.velocity.y * speed * Time.deltaTime), 0, QuadAllocator.GridSize - p.size);

			float t = p.life / p.maxLife;
			Color32 col = new Color32(255, 255, 255, (byte)(t * 255));
			WriteBlock(p.pos.x, p.pos.y, p.size, col);

			particles[i] = p;
		}

		allocator.ApplyToMesh(mesh);
	}

	private void TrySpawnParticle()
	{
		int size = Mathf.CeilToInt(Mathf.Sqrt(quadsPerParticle));
		Vector2 vel = Random.insideUnitCircle.normalized * 3f;

		for (int y = 0; y <= QuadAllocator.GridSize - size; y++)
			for (int x = 0; x <= QuadAllocator.GridSize - size; x++)
			{
				if (TryAllocBlock(x, y, size))
				{
					particles.Add(new Particle
					{
						life = lifetime,
						maxLife = lifetime,
						pos = new Vector2Int(x, y),
						velocity = vel,
						size = size
					});
					WriteBlock(x, y, size, Color.white);
					return;
				}
			}
	}

	private bool TryAllocBlock(int x, int y, int size)
	{
		for (int dy = 0; dy < size; dy++)
			for (int dx = 0; dx < size; dx++)
				if (!allocator.Allocate(x + dx, y + dy))
				{
					for (int ddy = 0; ddy < dy; ddy++)
						for (int ddx = 0; ddx < size; ddx++)
							allocator.Free(x + ddx, y + ddy);
					for (int ddx = 0; ddx < dx; ddx++)
						allocator.Free(x + ddx, y + dy);
					return false;
				}
		return true;
	}

	private void FreeParticle(Particle p)
	{
		for (int y = 0; y < p.size; y++)
			for (int x = 0; x < p.size; x++)
				allocator.Free(p.pos.x + x, p.pos.y + y);
	}

	private void WriteBlock(int gx, int gy, int size, Color32 col)
	{
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				int qx = gx + x;
				int qy = gy + y;
				int q = qy * QuadAllocator.GridSize + qx;
				int v = q * 4;

				ref var verts = ref allocator.Vertices;
				ref var cols = ref allocator.Colors;

				float px = qx * (0.12f + 0.02f);
				float py = qy * (0.12f + 0.02f);

				verts[v + 0] = new Vector3(px, py, 0);
				verts[v + 1] = new Vector3(px + 0.12f, py, 0);
				verts[v + 2] = new Vector3(px + 0.12f, py + 0.12f, 0);
				verts[v + 3] = new Vector3(px, py + 0.12f, 0);

				for (int i = 0; i < 4; i++) cols[v + i] = col;
			}
	}
}