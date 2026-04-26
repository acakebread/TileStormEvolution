using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SparkSystem : MonoBehaviour
{
	[System.Serializable]
	public class SparkSettings
	{
		public float speed = 4f;
		public float length = 0.1f; // Body length for simple sparks; body length for three-zone
		public float tipSize = 0.025f; // Head/tail quad size for three-zone
		public float lifetime = 1f;
		public float width = 0.01f;
		public bool decay = true; // Shrink all dimensions with age
		public bool useAdditiveBlending = false; // Toggle additive vs transparent blending
		public Color color = Color.white;
		public float gravity = 10f; // Y-axis damping
		public float moveScale = 1f; // Velocity scale
		public float bounceDamping = 0.8f; // Velocity damping on collision
		public float groundHeight = 0f; // Ground plane Y position
	}

	[SerializeField] private SparkSettings defaultSettings;
	[SerializeField] private int maxSparks = 4096;
	[SerializeField] private bool updateSparks = true;
	[SerializeField] private bool useGlobalGroundPlane = true;
	[SerializeField, Tooltip("Assign a URP material with 'Universal Render Pipeline/Particles/Unlit' set to Additive or Transparent")]
	private Material sparkMaterial;
	[SerializeField] private bool useThreeZoneSlicing = false;

	private class Spark
	{
		public Vector3 position; // World space
		public Vector3 previousPosition; // World space
		public Vector3 velocity; // Local space
		public float lifetime;
		public float maxLifetime;
		public Color color;
		public float width; // Current width
		public float initialWidth; // Initial width for decay
		public float tipSize; // Current tip size for three-zone
		public float initialTipSize; // Initial tip size
		public float length; // Current length for simple sparks
		public float initialLength; // Initial length
		public bool isActive;
		public int vertexIndex; // Starting vertex index in mesh
		public int poolIndex; // Index in sparkPool
	}

	private readonly float simSpeed = 1f;
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
			Material materialInstance = new Material(sparkMaterial);
			SetupURPMaterial(materialInstance);
			renderer.material = materialInstance;
			Debug.Log($"SparkSystem Awake: Material assigned, useAdditiveBlending={defaultSettings.useAdditiveBlending}, SrcBlend={materialInstance.GetInt("_SrcBlend")}, DstBlend={materialInstance.GetInt("_DstBlend")}, Keywords={string.Join(", ", materialInstance.shaderKeywords)}");
		}
		else
		{
			enabled = false;
			throw new System.Exception("SparkSystem: sparkMaterial is not assigned. Please assign a URP-compatible material with 'Universal Render Pipeline/Particles/Unlit'.");
		}
	}

	void Start()
	{
		MeshRenderer renderer = GetComponent<MeshRenderer>();
		if (renderer.material != null)
		{
			SetupURPMaterial(renderer.material);
			Debug.Log($"SparkSystem Start: Material reapplied, useAdditiveBlending={defaultSettings.useAdditiveBlending}, SrcBlend={renderer.material.GetInt("_SrcBlend")}, DstBlend={renderer.material.GetInt("_DstBlend")}, Keywords={string.Join(", ", renderer.material.shaderKeywords)}");
		}
	}

	void SetupURPMaterial(Material mat)
	{
		mat.DisableKeyword("_ALPHABLEND_ON");
		mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		mat.DisableKeyword("_ALPHATEST_ON");
		mat.DisableKeyword("_COLORADDSUBDIFF_ON");

		if (defaultSettings.useAdditiveBlending)
		{
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
			mat.SetInt("_ZWrite", 0);
			mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
		}
		else
		{
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			mat.SetInt("_ZWrite", 0);
			mat.EnableKeyword("_ALPHABLEND_ON");
		}

		mat.SetColor("_BaseColor", Color.white);
		mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		mat.SetFloat("_Cutoff", 0f);
		mat.SetFloat("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
		mat.SetPass(0);
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

		spark.position = position + transform.position; // World space
		spark.previousPosition = spark.position;
		spark.velocity = transform.InverseTransformDirection(velocity) * settings.speed; // Respect velocity magnitude
		spark.lifetime = settings.lifetime;
		spark.maxLifetime = settings.lifetime;
		spark.color = settings.color;
		spark.width = settings.width;
		spark.initialWidth = settings.width;
		spark.tipSize = settings.tipSize;
		spark.initialTipSize = settings.tipSize;
		spark.length = settings.length;
		spark.initialLength = settings.length;
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
				continue;
			}

			if (!defaultSettings.useAdditiveBlending)
			{
				spark.color.a = spark.lifetime / spark.maxLifetime;
			}
			else
			{
				spark.color.a = 1f; // Alpha irrelevant for additive
			}

			if (defaultSettings.decay)
			{
				float decayFactor = spark.lifetime / spark.maxLifetime;
				spark.width = spark.initialWidth * decayFactor;
				spark.tipSize = spark.initialTipSize * decayFactor;
				spark.length = spark.initialLength * decayFactor;
			}

			spark.velocity.y -= defaultSettings.gravity * deltaTime * simSpeed;
			spark.previousPosition = spark.position;
			spark.position += spark.velocity * defaultSettings.moveScale * deltaTime * simSpeed;

			// Ground collision
			float currentY = useGlobalGroundPlane ? spark.position.y : transform.InverseTransformPoint(spark.position).y;
			float groundY = defaultSettings.groundHeight;
			if (spark.velocity.y < 0 && currentY <= groundY)
			{
				if (useGlobalGroundPlane)
				{
					spark.position.y = groundY;
				}
				else
				{
					Vector3 localPos = transform.InverseTransformPoint(spark.position);
					localPos.y = groundY;
					spark.position = transform.TransformPoint(localPos);
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
				colors[vertexIndex + i] = defaultSettings.useAdditiveBlending ? Color.black : Color.clear;
		}
		else
		{
			for (int i = 0; i < 6; i++)
				triangles[indexOffset + i] = vertexIndex;
			for (int i = 0; i < 4; i++)
				colors[vertexIndex + i] = defaultSettings.useAdditiveBlending ? Color.black : Color.clear;
		}
	}

	private void UpdateMesh()
	{
		Vector3 camPos = mainCamera.transform.position;
		Vector3 camRight = mainCamera.transform.right;
		Vector3 camUp = mainCamera.transform.up;

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

			Vector3 pos = spark.position;
			Vector3 prevPos = spark.previousPosition;

			Vector3 sparkDir = (pos - prevPos);
			if (sparkDir.sqrMagnitude < 0.0001f)
				sparkDir = spark.velocity.normalized;
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
			Vector3 vecy = sy * spark.width * 0.5f;
			Vector3 tipVec = sparkDir * spark.tipSize;

			int vertexIndex = spark.vertexIndex;
			if (useThreeZoneSlicing)
			{
				Vector3 tailTail = prevPos - tipVec;
				Vector3 tailFront = prevPos;
				vertices[vertexIndex + 0] = tailTail - vecy;
				vertices[vertexIndex + 1] = tailTail + vecy;
				vertices[vertexIndex + 2] = tailFront - vecy;
				vertices[vertexIndex + 3] = tailFront + vecy;

				vertices[vertexIndex + 2] = vertices[vertexIndex + 2];
				vertices[vertexIndex + 3] = vertices[vertexIndex + 3];
				vertices[vertexIndex + 4] = pos - vecy;
				vertices[vertexIndex + 5] = pos + vecy;

				Vector3 headTail = pos;
				Vector3 headFront = pos + tipVec;
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
					colors[vertexIndex + j] = spark.color;
			}
			else
			{
				float extension = spark.length * 0.5f;
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
			}
		}

		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0);
		mesh.SetColors(colors);
		mesh.RecalculateBounds();
	}
}