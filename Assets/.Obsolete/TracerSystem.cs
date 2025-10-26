//using UnityEngine;
//using System.Collections.Generic;

//[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
//public class TracerSystem : MonoBehaviour
//{
//	[System.Serializable]
//	public class TracerSettings
//	{
//		public float speed = 12f;
//		public float length = 12.5f; // Used to extend tracer forward and backward
//		public float lifetime = 1f;
//		public float width = 1.25f;
//		public Color color = Color.white;
//	}

//	[SerializeField] public TracerSettings defaultSettings;
//	[SerializeField] private int maxTracers = 256;
//	[SerializeField, Tooltip("Assign a URP material with 'Universal Render Pipeline/Particles/Unlit' (Transparent, Fade mode)")]
//	private Material tracerMaterial; // Must assign a URP-compatible material

//	private class Tracer
//	{
//		public Vector3 position;
//		public Vector3 previousPosition;
//		public Vector3 velocity;
//		public float lifetime;
//		public float maxLifetime;
//		public Color color;
//		public float width;
//		public bool isActive;
//	}

//	private List<Tracer> tracerPool;
//	private Mesh mesh;
//	private List<Vector3> vertices;
//	private List<int> triangles;
//	private List<Color> colors;
//	private List<Vector2> uvs;
//	private Camera mainCamera;

//	void Awake()
//	{
//		InitializePool();
//		InitializeMesh();
//		mainCamera = Camera.main;

//		// Validate and assign tracerMaterial
//		MeshRenderer renderer = GetComponent<MeshRenderer>();
//		if (tracerMaterial != null)
//		{
//			SetupURPMaterial(tracerMaterial);
//			renderer.material = tracerMaterial;
//		}
//		else
//		{
//			enabled = false;
//			throw new System.Exception("TracerSystem: tracerMaterial is not assigned. Please assign a URP-compatible material with 'Universal Render Pipeline/Particles/Unlit' (Transparent, Fade mode).");
//		}
//	}

//	void SetupURPMaterial(Material mat)
//	{
//		// Configure material for URP transparency with vertex color support
//		mat.SetFloat("_Mode", 2); // Fade mode
//		mat.SetInt("_Surface", 1); // Transparent surface
//		mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
//		mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
//		mat.SetInt("_ZWrite", 0); // Disable ZWrite
//		mat.EnableKeyword("_ALPHABLEND_ON");
//		mat.SetColor("_BaseColor", Color.white); // Ensure vertex colors control fading
//		mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // Transparent queue
//	}

//	void InitializePool()
//	{
//		tracerPool = new List<Tracer>(maxTracers);
//		for (int i = 0; i < maxTracers; i++)
//		{
//			tracerPool.Add(new Tracer { isActive = false });
//		}
//	}

//	void InitializeMesh()
//	{
//		mesh = new Mesh { name = "TracerMesh" };
//		GetComponent<MeshFilter>().mesh = mesh;
//		vertices = new List<Vector3>();
//		triangles = new List<int>();
//		colors = new List<Color>();
//		uvs = new List<Vector2>();
//	}

//	void Update()
//	{
//		UpdateTracers();
//		RebuildMesh();
//	}

//	public void SpawnTracer(Vector3 position, Vector3 direction, TracerSettings settings = null)
//	{
//		if (settings == null) settings = defaultSettings;

//		Tracer tracer = GetInactiveTracer();
//		if (tracer == null) return;

//		tracer.position = position;
//		tracer.previousPosition = position;
//		tracer.velocity = direction.normalized * settings.speed;
//		tracer.lifetime = settings.lifetime;
//		tracer.maxLifetime = settings.lifetime;
//		tracer.color = settings.color;
//		tracer.width = settings.width;
//		tracer.isActive = true;
//	}

//	Tracer GetInactiveTracer()
//	{
//		foreach (Tracer tracer in tracerPool)
//		{
//			if (!tracer.isActive) return tracer;
//		}
//		return null; // Pool exhausted
//	}

//	void UpdateTracers()
//	{
//		float deltaTime = Time.deltaTime;

//		foreach (Tracer tracer in tracerPool)
//		{
//			if (!tracer.isActive) continue;

//			// Update position
//			tracer.previousPosition = tracer.position;
//			tracer.position += tracer.velocity * deltaTime;

//			// Update lifetime
//			tracer.lifetime -= deltaTime;
//			if (tracer.lifetime <= 0f)
//			{
//				tracer.isActive = false;
//				continue;
//			}

//			// Fade color based on lifetime
//			float alpha = tracer.lifetime / tracer.maxLifetime;
//			tracer.color.a = alpha;
//		}
//	}

//	void RebuildMesh()
//	{
//		vertices.Clear();
//		triangles.Clear();
//		colors.Clear();
//		uvs.Clear();

//		int activeTracers = 0;
//		foreach (Tracer tracer in tracerPool)
//		{
//			if (!tracer.isActive) continue;

//			// Calculate tracer direction (from tail to head)
//			Vector3 tracerDir = (tracer.position - tracer.previousPosition);
//			if (tracerDir.sqrMagnitude < 0.0001f) // Avoid division by zero
//				tracerDir = tracer.velocity.normalized * 0.01f;
//			else
//				tracerDir = tracerDir.normalized;

//			// Camera direction for billboard (from tracer to camera)
//			Vector3 camPos = mainCamera.transform.position;
//			Vector3 toCamera = (camPos - tracer.position).normalized;

//			// Compute right vector for quad width
//			Vector3 right = Vector3.Cross(toCamera, tracerDir).normalized * tracer.width;

//			// Extend tracer forward and backward
//			float extension = defaultSettings.length * 0.5f;
//			Vector3 tailPos = tracer.previousPosition - tracerDir * extension;
//			Vector3 headPos = tracer.position + tracerDir * extension;

//			// Define quad vertices
//			Vector3 v0 = tailPos - right; // Bottom-left (tail)
//			Vector3 v1 = tailPos + right; // Bottom-right (tail)
//			Vector3 v2 = headPos - right; // Top-left (head)
//			Vector3 v3 = headPos + right; // Top-right (head)

//			int vertexOffset = activeTracers * 4;
//			vertices.Add(v0);
//			vertices.Add(v1);
//			vertices.Add(v2);
//			vertices.Add(v3);

//			// Define triangles
//			triangles.Add(vertexOffset + 0);
//			triangles.Add(vertexOffset + 2);
//			triangles.Add(vertexOffset + 1);
//			triangles.Add(vertexOffset + 1);
//			triangles.Add(vertexOffset + 2);
//			triangles.Add(vertexOffset + 3);

//			// Assign colors with alpha
//			colors.Add(tracer.color);
//			colors.Add(tracer.color);
//			colors.Add(tracer.color);
//			colors.Add(tracer.color);

//			// UVs for texturing
//			uvs.Add(new Vector2(0, 0));
//			uvs.Add(new Vector2(1, 0));
//			uvs.Add(new Vector2(0, 1));
//			uvs.Add(new Vector2(1, 1));

//			activeTracers++;
//		}

//		// Update mesh
//		mesh.Clear();
//		mesh.SetVertices(vertices);
//		mesh.SetTriangles(triangles, 0);
//		mesh.SetColors(colors);
//		mesh.SetUVs(0, uvs);
//		mesh.RecalculateBounds();
//	}
//}