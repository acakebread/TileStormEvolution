using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SparkSystem : MonoBehaviour
{
	[System.Serializable]
	public class SparkSettings
	{
		public float speed = 8f;
		public float length = 0.1f; // Extends spark forward and backward
		public float lifetime = 3f;
		public float width = 0.05f;
		public Color color = Color.white;
		public float gravity = 10f; // Y-axis damping
		public float moveScale = 1f; // Velocity scale
		public float bounceDamping = 0.8f; // Velocity damping on collision
		public float groundHeight = 0f; // Ground plane Y position
	}

	[SerializeField] private SparkSettings defaultSettings;
	[SerializeField] private int maxSparks = 256;
	[SerializeField] private bool updateSparks = true;
	[SerializeField] private bool useGlobalGroundPlane = true;
	[SerializeField, Tooltip("Assign a URP material with 'Universal Render Pipeline/Particles/Unlit' (Transparent, Fade mode)")]
	private Material sparkMaterial;

	private class Spark
	{
		public Vector3 position; // Local space
		public Vector3 previousPosition; // Local space
		public Vector3 velocity; // Local space
		public float lifetime;
		public float maxLifetime;
		public Color color;
		public float width;
		public bool isActive;
		public int vertexIndex; // Starting vertex index in mesh
		public int poolIndex; // Index in sparkPool
	}

	private readonly float simSpeed = 1f; // SIM_SPEED from DirectX sample
	private List<Spark> sparkPool;
	private List<Spark> activeSparks;
	private List<int> freeSparkIndices;
	private Mesh mesh;
	private List<Vector3> vertices;
	private List<int> triangles;
	private List<Color> colors;
	private List<Vector2> uvs;
	private Camera mainCamera;
	private int activeSparkCount;

	void Awake()
	{
		InitializePool();
		InitializeMesh();
		mainCamera = Camera.main;

		MeshRenderer renderer = GetComponent<MeshRenderer>();
		if (sparkMaterial != null)
		{
			SetupURPMaterial(sparkMaterial);
			renderer.material = sparkMaterial;
		}
		else
		{
			enabled = false;
			throw new System.Exception("SparkSystem: sparkMaterial is not assigned. Please assign a URP-compatible material with 'Universal Render Pipeline/Particles/Unlit' (Transparent, Fade mode).");
		}
	}

	void SetupURPMaterial(Material mat)
	{
		mat.SetFloat("_Mode", 2); // Fade mode
		mat.SetInt("_Surface", 1); // Transparent surface
		mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		mat.SetInt("_ZWrite", 0); // Disable ZWrite
		mat.EnableKeyword("_ALPHABLEND_ON");
		mat.SetColor("_BaseColor", Color.white); // Ensure vertex colors control fading
		mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
	}

	void InitializePool()
	{
		sparkPool = new List<Spark>(maxSparks);
		activeSparks = new List<Spark>(maxSparks);
		freeSparkIndices = new List<int>(maxSparks);
		for (int i = 0; i < maxSparks; i++)
		{
			sparkPool.Add(new Spark { isActive = false, vertexIndex = i * 4, poolIndex = i });
			freeSparkIndices.Add(i);
		}
		activeSparkCount = 0;
	}

	void InitializeMesh()
	{
		mesh = new Mesh { name = "SparkMesh" };
		GetComponent<MeshFilter>().mesh = mesh;
		vertices = new List<Vector3>(maxSparks * 4);
		triangles = new List<int>(maxSparks * 6);
		colors = new List<Color>(maxSparks * 4);
		uvs = new List<Vector2>(maxSparks * 4);

		// Preallocate mesh data with degenerate quads
		for (int i = 0; i < maxSparks; i++)
		{
			int vertexOffset = i * 4;
			vertices.AddRange(new[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });
			triangles.AddRange(new[] { vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset });
			colors.AddRange(new[] { Color.clear, Color.clear, Color.clear, Color.clear });
			uvs.AddRange(new[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero });
		}
		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		mesh.SetColors(colors);
		mesh.SetUVs(0, uvs);
		mesh.RecalculateBounds();
	}

	void Update()
	{
		if (updateSparks) UpdateSparks();
		UpdateMesh();
	}

	public void SpawnSpark(Vector3 position, Vector3 velocity, SparkSettings settings = null)
	{
		if (!updateSparks) return;
		if (settings == null) settings = defaultSettings;

		Spark spark = GetInactiveSpark();
		if (spark == null) return;

		// Initialize spark in local space
		spark.position = position;
		spark.previousPosition = position;
		spark.velocity = velocity.normalized * settings.speed;
		spark.lifetime = settings.lifetime; // Supports variable lifetimes
		spark.maxLifetime = settings.lifetime;
		spark.color = settings.color;
		spark.width = settings.width;
		spark.isActive = true;

		// Add to active sparks
		activeSparks.Add(spark);
		activeSparkCount++;
	}

	private Spark GetInactiveSpark()
	{
		if (freeSparkIndices.Count == 0) return null; // Pool exhausted

		// Take the last free index (O(1))
		int poolIndex = freeSparkIndices[freeSparkIndices.Count - 1];
		freeSparkIndices.RemoveAt(freeSparkIndices.Count - 1);
		return sparkPool[poolIndex];
	}

	private void UpdateSparks()
	{
		float deltaTime = Time.deltaTime;

		// Update and defragment active sparks, iterating backward
		for (int i = activeSparkCount - 1; i >= 0; i--)
		{
			if (i >= activeSparks.Count)
			{
				Debug.LogWarning($"Index {i} out of bounds for activeSparks.Count {activeSparks.Count}, activeSparkCount {activeSparkCount}");
				activeSparkCount = activeSparks.Count; // Sync counts
				break;
			}

			Spark spark = activeSparks[i];
			if (!spark.isActive)
			{
				activeSparks.RemoveAt(i);
				activeSparkCount--;
				Debug.Log($"Removed inactive spark at index {i}, new activeSparkCount: {activeSparkCount}");
				continue;
			}

			// Update lifetime
			spark.lifetime -= deltaTime;
			if (spark.lifetime <= 0f)
			{
				// Deactivate and defragment
				spark.isActive = false;
				DeactivateQuad(spark.vertexIndex);
				freeSparkIndices.Add(spark.poolIndex);
				activeSparks.RemoveAt(i);
				activeSparkCount--;
				//Debug.Log($"Deactivated spark at index {i}, vertexIndex {spark.vertexIndex}, poolIndex {spark.poolIndex}, new activeSparkCount: {activeSparkCount}");
				continue;
			}

			// Fade color
			spark.color.a = spark.lifetime / spark.maxLifetime;

			// Apply gravity (in local space)
			spark.velocity.y -= defaultSettings.gravity * deltaTime * simSpeed;

			// Update position (in local space)
			spark.previousPosition = spark.position;
			spark.position += spark.velocity * defaultSettings.moveScale * deltaTime * simSpeed;

			// Ground collision
			float collide = spark.width * 0.75f;
			float currentY = useGlobalGroundPlane ? transform.TransformPoint(spark.position).y : spark.position.y;
			float groundY = defaultSettings.groundHeight;
			if (spark.velocity.y < 0 && currentY < groundY + collide)
			{
				if (useGlobalGroundPlane)
				{
					Vector3 worldPos = transform.TransformPoint(spark.position);
					worldPos.y = groundY + (collide * 2f - (worldPos.y - groundY));
					spark.position = transform.InverseTransformPoint(worldPos);
				}
				else
				{
					spark.position.y = groundY + (collide * 2f - (spark.position.y - groundY));
				}
				spark.velocity.y = -spark.velocity.y * defaultSettings.bounceDamping;
			}
		}
	}

	private void DeactivateQuad(int vertexIndex)
	{
		// Make quad degenerate
		int indexOffset = (vertexIndex / 4) * 6;
		triangles[indexOffset + 0] = vertexIndex;
		triangles[indexOffset + 1] = vertexIndex;
		triangles[indexOffset + 2] = vertexIndex;
		triangles[indexOffset + 3] = vertexIndex;
		triangles[indexOffset + 4] = vertexIndex;
		triangles[indexOffset + 5] = vertexIndex;

		// Set colors to transparent
		colors[vertexIndex + 0] = Color.clear;
		colors[vertexIndex + 1] = Color.clear;
		colors[vertexIndex + 2] = Color.clear;
		colors[vertexIndex + 3] = Color.clear;
	}

	private void UpdateQuadVertexIndex(int oldVertexIndex, int newVertexIndex)
	{
		// Move vertex, color, and UV data to new vertex index
		vertices[newVertexIndex + 0] = vertices[oldVertexIndex + 0];
		vertices[newVertexIndex + 1] = vertices[oldVertexIndex + 1];
		vertices[newVertexIndex + 2] = vertices[oldVertexIndex + 2];
		vertices[newVertexIndex + 3] = vertices[oldVertexIndex + 3];

		colors[newVertexIndex + 0] = colors[oldVertexIndex + 0];
		colors[newVertexIndex + 1] = colors[oldVertexIndex + 1];
		colors[newVertexIndex + 2] = colors[oldVertexIndex + 2];
		colors[newVertexIndex + 3] = colors[oldVertexIndex + 3];

		uvs[newVertexIndex + 0] = uvs[oldVertexIndex + 0];
		uvs[newVertexIndex + 1] = uvs[oldVertexIndex + 1];
		uvs[newVertexIndex + 2] = uvs[oldVertexIndex + 2];
		uvs[newVertexIndex + 3] = uvs[oldVertexIndex + 3];

		// Update triangle indices
		int newIndexOffset = (newVertexIndex / 4) * 6;
		triangles[newIndexOffset + 0] = newVertexIndex + 0;
		triangles[newIndexOffset + 1] = newVertexIndex + 1;
		triangles[newIndexOffset + 2] = newVertexIndex + 2;
		triangles[newIndexOffset + 3] = newVertexIndex + 1;
		triangles[newIndexOffset + 4] = newVertexIndex + 3;
		triangles[newIndexOffset + 5] = newVertexIndex + 2;

		// Deactivate old quad
		DeactivateQuad(oldVertexIndex);
	}

	private void UpdateMesh()
	{
		Vector3 camPos = mainCamera.transform.position;
		Vector3 camRight = mainCamera.transform.right;
		Vector3 camUp = mainCamera.transform.up;

		// Update only active sparks
		for (int i = 0; i < activeSparkCount; i++)
		{
			if (i >= activeSparks.Count)
			{
				Debug.LogWarning($"UpdateMesh: Index {i} out of bounds for activeSparks.Count {activeSparks.Count}, activeSparkCount {activeSparkCount}");
				activeSparkCount = activeSparks.Count;
				break;
			}

			Spark spark = activeSparks[i];
			if (!spark.isActive) continue;

			// Transform positions to world space for billboarding
			Vector3 worldPos = transform.TransformPoint(spark.position);
			Vector3 worldPrevPos = transform.TransformPoint(spark.previousPosition);

			// Calculate spark direction (Vnow to Vold)
			Vector3 sparkDir = (worldPos - worldPrevPos);
			if (sparkDir.sqrMagnitude < 0.0001f)
				sparkDir = transform.TransformDirection(spark.velocity).normalized;
			else
				sparkDir = sparkDir.normalized;

			// Compute billboard vectors (mimic MHStrip SetPosition)
			Vector3 sz = worldPos - camPos;
			Vector3 sy = Vector3.Cross(sz, sparkDir);
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
			Vector3 vecx = sx * spark.width * 0.5f;
			Vector3 vecy = sy * spark.width * 0.5f;

			// Extend along direction
			float extension = defaultSettings.length * 0.5f;
			Vector3 tailPos = worldPrevPos - sparkDir * extension;
			Vector3 headPos = worldPos + sparkDir * extension;

			// Define quad vertices
			Vector3 v0 = tailPos - vecy - vecx;
			Vector3 v1 = tailPos + vecy - vecx;
			Vector3 v2 = headPos - vecy + vecx;
			Vector3 v3 = headPos + vecy + vecx;

			// Transform vertices back to local space
			int vertexIndex = spark.vertexIndex;
			vertices[vertexIndex + 0] = transform.InverseTransformPoint(v0);
			vertices[vertexIndex + 1] = transform.InverseTransformPoint(v1);
			vertices[vertexIndex + 2] = transform.InverseTransformPoint(v2);
			vertices[vertexIndex + 3] = transform.InverseTransformPoint(v3);

			// Update triangles
			int indexOffset = (vertexIndex / 4) * 6;
			triangles[indexOffset + 0] = vertexIndex + 0;
			triangles[indexOffset + 1] = vertexIndex + 1;
			triangles[indexOffset + 2] = vertexIndex + 2;
			triangles[indexOffset + 3] = vertexIndex + 1;
			triangles[indexOffset + 4] = vertexIndex + 3;
			triangles[indexOffset + 5] = vertexIndex + 2;

			// Update colors
			colors[vertexIndex + 0] = spark.color;
			colors[vertexIndex + 1] = spark.color;
			colors[vertexIndex + 2] = spark.color;
			colors[vertexIndex + 3] = spark.color;

			// Update UVs
			uvs[vertexIndex + 0] = new Vector2(0f, 0f);
			uvs[vertexIndex + 1] = new Vector2(1f, 0f);
			uvs[vertexIndex + 2] = new Vector2(0f, 1f);
			uvs[vertexIndex + 3] = new Vector2(1f, 1f);
		}

		// Update mesh
		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		mesh.SetColors(colors);
		mesh.SetUVs(0, uvs);
		mesh.RecalculateBounds();
	}
}