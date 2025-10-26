using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SparkSystem : MonoBehaviour
{
	[System.Serializable]
	public class SparkSettings
	{
		public float speed = 8f;
		public float length = 0.1f; // Body length for simple sparks; body length for three-zone
		public float tipSize = 0.025f; // Head/tail quad size for three-zone
		public float lifetime = 3f;
		public float width = 0.05f;
		public Color color = Color.white;
		public float gravity = 10f; // Y-axis damping
		public float moveScale = 1f; // Velocity scale
		public float bounceDamping = 0.8f; // Velocity damping on collision
		public float groundHeight = 0f; // Ground plane Y position
		public Vector2 headUVRange = new Vector2(0f, 0.333f); // UV x-range for head
		public Vector2 bodyUVRange = new Vector2(0.333f, 0.666f); // UV x-range for body
		public Vector2 tailUVRange = new Vector2(0.666f, 1f); // UV x-range for tail
	}

	[SerializeField] private SparkSettings defaultSettings;
	[SerializeField] private int maxSparks = 256;
	[SerializeField] private bool updateSparks = true;
	[SerializeField] private bool useGlobalGroundPlane = true; // Default to global ground plane
	[SerializeField, Tooltip("Assign a URP material with 'Universal Render Pipeline/Particles/Unlit' (Transparent, Fade mode)")]
	private Material sparkMaterial;
	[SerializeField] private bool useThreeZoneSlicing = false; // Toggle simple vs. three-zone sparks

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
	private int verticesPerSpark => useThreeZoneSlicing ? 8 : 4;
	private int trianglesPerSpark => useThreeZoneSlicing ? 18 : 6;

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
			sparkPool.Add(new Spark { isActive = false, vertexIndex = i * verticesPerSpark, poolIndex = i });
			freeSparkIndices.Add(i);
		}
		activeSparkCount = 0;
	}

	void InitializeMesh()
	{
		mesh = new Mesh { name = "SparkMesh" };
		GetComponent<MeshFilter>().mesh = mesh;
		vertices = new List<Vector3>(maxSparks * verticesPerSpark);
		triangles = new List<int>(maxSparks * trianglesPerSpark);
		colors = new List<Color>(maxSparks * verticesPerSpark);
		uvs = new List<Vector2>(maxSparks * verticesPerSpark);

		for (int i = 0; i < maxSparks; i++)
		{
			int vertexOffset = i * verticesPerSpark;
			if (useThreeZoneSlicing)
			{
				vertices.AddRange(new Vector3[8]);
				triangles.AddRange(new[] {
					vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset, // Tail quad
                    vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset, // Body quad
                    vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset  // Head quad
                });
				colors.AddRange(new Color[8]);
				uvs.AddRange(new Vector2[8]);
			}
			else
			{
				vertices.AddRange(new Vector3[4]);
				triangles.AddRange(new[] { vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset, vertexOffset });
				colors.AddRange(new Color[4]);
				uvs.AddRange(new Vector2[4]);
			}
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

		// Convert world-space position to local space
		spark.position = position + transform.position;
		spark.previousPosition = spark.position;
		spark.velocity = transform.InverseTransformDirection(velocity.normalized) * settings.speed;
		spark.lifetime = settings.lifetime;
		spark.maxLifetime = settings.lifetime;
		spark.color = settings.color;
		spark.width = settings.width;
		spark.isActive = true;

		activeSparks.Add(spark);
		activeSparkCount++;
	}

	private Spark GetInactiveSpark()
	{
		if (freeSparkIndices.Count == 0) return null;

		int poolIndex = freeSparkIndices[freeSparkIndices.Count - 1];
		freeSparkIndices.RemoveAt(freeSparkIndices.Count - 1);
		return sparkPool[poolIndex];
	}

	private void UpdateSparks()
	{
		float deltaTime = Time.deltaTime;

		for (int i = activeSparkCount - 1; i >= 0; i--)
		{
			if (i >= activeSparks.Count)
			{
				Debug.LogWarning($"Index {i} out of bounds for activeSparks.Count {activeSparks.Count}, activeSparkCount {activeSparkCount}");
				activeSparkCount = activeSparks.Count;
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

			spark.lifetime -= deltaTime;
			if (spark.lifetime <= 0f)
			{
				spark.isActive = false;
				DeactivateQuad(spark.vertexIndex);
				freeSparkIndices.Add(spark.poolIndex);
				activeSparks.RemoveAt(i);
				activeSparkCount--;
				//Debug.Log($"Deactivated spark at index {i}, vertexIndex {spark.vertexIndex}, poolIndex {spark.poolIndex}, new activeSparkCount: {activeSparkCount}");
				continue;
			}

			spark.color.a = spark.lifetime / spark.maxLifetime;
			spark.velocity.y -= defaultSettings.gravity * deltaTime * simSpeed;
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
		int indexOffset = (vertexIndex / verticesPerSpark) * trianglesPerSpark;
		if (useThreeZoneSlicing)
		{
			for (int i = 0; i < 18; i++)
				triangles[indexOffset + i] = vertexIndex;
			for (int i = 0; i < 8; i++)
				colors[vertexIndex + i] = Color.clear;
		}
		else
		{
			for (int i = 0; i < 6; i++)
				triangles[indexOffset + i] = vertexIndex;
			for (int i = 0; i < 4; i++)
				colors[vertexIndex + i] = Color.clear;
		}
	}

	// Kept for potential future use (e.g., vertex index reassignment), currently unused
	private void UpdateQuadVertexIndex(int oldVertexIndex, int newVertexIndex)
	{
		for (int i = 0; i < verticesPerSpark; i++)
		{
			vertices[newVertexIndex + i] = vertices[oldVertexIndex + i];
			colors[newVertexIndex + i] = colors[oldVertexIndex + i];
			uvs[newVertexIndex + i] = uvs[oldVertexIndex + i];
		}

		int newIndexOffset = (newVertexIndex / verticesPerSpark) * trianglesPerSpark;
		if (useThreeZoneSlicing)
		{
			triangles[newIndexOffset + 0] = newVertexIndex + 0;
			triangles[newIndexOffset + 1] = newVertexIndex + 1;
			triangles[newIndexOffset + 2] = newVertexIndex + 2;
			triangles[newIndexOffset + 3] = newVertexIndex + 1;
			triangles[newIndexOffset + 4] = newVertexIndex + 3;
			triangles[newIndexOffset + 5] = newVertexIndex + 2;
			triangles[newIndexOffset + 6] = newVertexIndex + 2;
			triangles[newIndexOffset + 7] = newVertexIndex + 3;
			triangles[newIndexOffset + 8] = newVertexIndex + 4;
			triangles[newIndexOffset + 9] = newVertexIndex + 3;
			triangles[newIndexOffset + 10] = newVertexIndex + 5;
			triangles[newIndexOffset + 11] = newVertexIndex + 4;
			triangles[newIndexOffset + 12] = newVertexIndex + 4;
			triangles[newIndexOffset + 13] = newVertexIndex + 5;
			triangles[newIndexOffset + 14] = newVertexIndex + 6;
			triangles[newIndexOffset + 15] = newVertexIndex + 5;
			triangles[newIndexOffset + 16] = newVertexIndex + 7;
			triangles[newIndexOffset + 17] = newVertexIndex + 6;
		}
		else
		{
			triangles[newIndexOffset + 0] = newVertexIndex + 0;
			triangles[newIndexOffset + 1] = newVertexIndex + 1;
			triangles[newIndexOffset + 2] = newVertexIndex + 2;
			triangles[newIndexOffset + 3] = newVertexIndex + 1;
			triangles[newIndexOffset + 4] = newVertexIndex + 3;
			triangles[newIndexOffset + 5] = newVertexIndex + 2;
		}

		DeactivateQuad(oldVertexIndex);
	}

	private void UpdateMesh()
	{
		Vector3 camPos = transform.InverseTransformPoint(mainCamera.transform.position); // Camera in local space
		Vector3 camRight = transform.InverseTransformDirection(mainCamera.transform.right);
		Vector3 camUp = transform.InverseTransformDirection(mainCamera.transform.up);

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

			Vector3 pos = spark.position; // Local space
			Vector3 prevPos = spark.previousPosition;

			Vector3 sparkDir = (pos - prevPos);
			if (sparkDir.sqrMagnitude < 0.0001f)
				sparkDir = spark.velocity.normalized; // Local space
			else
				sparkDir = sparkDir.normalized;

			Vector3 sz = pos - camPos;
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
			Vector3 vecy = sy * spark.width * 0.5f; // Width offset
			Vector3 tipVec = sparkDir * defaultSettings.tipSize; // Head/tail extension

			int vertexIndex = spark.vertexIndex;
			if (useThreeZoneSlicing)
			{
				// Tail quad (v0, v1, v2, v3): from prevPos - tipSize to prevPos
				Vector3 tailTail = prevPos - tipVec;
				Vector3 tailFront = prevPos;
				vertices[vertexIndex + 0] = tailTail - vecy;
				vertices[vertexIndex + 1] = tailTail + vecy;
				vertices[vertexIndex + 2] = tailFront - vecy;
				vertices[vertexIndex + 3] = tailFront + vecy;

				// Body quad (v2, v3, v4, v5): from prevPos to pos
				vertices[vertexIndex + 2] = vertices[vertexIndex + 2]; // Shared with tail
				vertices[vertexIndex + 3] = vertices[vertexIndex + 3]; // Shared with tail
				vertices[vertexIndex + 4] = pos - vecy;
				vertices[vertexIndex + 5] = pos + vecy;

				// Head quad (v4, v5, v6, v7): from pos to pos + tipSize
				Vector3 headTail = pos;
				Vector3 headFront = pos + tipVec;
				vertices[vertexIndex + 4] = vertices[vertexIndex + 4]; // Shared with body
				vertices[vertexIndex + 5] = vertices[vertexIndex + 5]; // Shared with body
				vertices[vertexIndex + 6] = headFront - vecy;
				vertices[vertexIndex + 7] = headFront + vecy;

				// Update triangles
				int indexOffset = (vertexIndex / 8) * 18;
				// Tail quad
				triangles[indexOffset + 0] = vertexIndex + 0;
				triangles[indexOffset + 1] = vertexIndex + 1;
				triangles[indexOffset + 2] = vertexIndex + 2;
				triangles[indexOffset + 3] = vertexIndex + 1;
				triangles[indexOffset + 4] = vertexIndex + 3;
				triangles[indexOffset + 5] = vertexIndex + 2;
				// Body quad
				triangles[indexOffset + 6] = vertexIndex + 2;
				triangles[indexOffset + 7] = vertexIndex + 3;
				triangles[indexOffset + 8] = vertexIndex + 4;
				triangles[indexOffset + 9] = vertexIndex + 3;
				triangles[indexOffset + 10] = vertexIndex + 5;
				triangles[indexOffset + 11] = vertexIndex + 4;
				// Head quad
				triangles[indexOffset + 12] = vertexIndex + 4;
				triangles[indexOffset + 13] = vertexIndex + 5;
				triangles[indexOffset + 14] = vertexIndex + 6;
				triangles[indexOffset + 15] = vertexIndex + 5;
				triangles[indexOffset + 16] = vertexIndex + 7;
				triangles[indexOffset + 17] = vertexIndex + 6;

				// Update colors
				for (int j = 0; j < 8; j++)
					colors[vertexIndex + j] = spark.color;

				// Update UVs
				uvs[vertexIndex + 0] = new Vector2(defaultSettings.tailUVRange.x, 0f);
				uvs[vertexIndex + 1] = new Vector2(defaultSettings.tailUVRange.y, 0f);
				uvs[vertexIndex + 2] = new Vector2(defaultSettings.tailUVRange.x, 1f);
				uvs[vertexIndex + 3] = new Vector2(defaultSettings.tailUVRange.y, 1f);
				uvs[vertexIndex + 2] = new Vector2(defaultSettings.bodyUVRange.x, 0f); // Shared
				uvs[vertexIndex + 3] = new Vector2(defaultSettings.bodyUVRange.y, 0f); // Shared
				uvs[vertexIndex + 4] = new Vector2(defaultSettings.bodyUVRange.x, 1f);
				uvs[vertexIndex + 5] = new Vector2(defaultSettings.bodyUVRange.y, 1f);
				uvs[vertexIndex + 4] = new Vector2(defaultSettings.headUVRange.x, 0f); // Shared
				uvs[vertexIndex + 5] = new Vector2(defaultSettings.headUVRange.y, 0f); // Shared
				uvs[vertexIndex + 6] = new Vector2(defaultSettings.headUVRange.x, 1f);
				uvs[vertexIndex + 7] = new Vector2(defaultSettings.headUVRange.y, 1f);
			}
			else
			{
				float extension = defaultSettings.length * 0.5f;
				Vector3 tailPos = prevPos - sparkDir * extension;
				Vector3 headPos = pos + sparkDir * extension;

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
					colors[vertexIndex + j] = spark.color;

				uvs[vertexIndex + 0] = new Vector2(0f, 0f);
				uvs[vertexIndex + 1] = new Vector2(1f, 0f);
				uvs[vertexIndex + 2] = new Vector2(0f, 1f);
				uvs[vertexIndex + 3] = new Vector2(1f, 1f);
			}
		}

		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		mesh.SetColors(colors);
		mesh.SetUVs(0, uvs);
		mesh.RecalculateBounds();
	}
}