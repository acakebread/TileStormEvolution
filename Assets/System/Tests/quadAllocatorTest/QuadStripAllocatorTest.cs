using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MassiveHadronLtd;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class QuadStripAllocatorTest : MonoBehaviour
{
	private QuadStripAllocator quadAllocator;
	private Mesh mesh;
	private MeshFilter meshFilter;
	private MeshRenderer meshRenderer;
	private Camera cam;

	private readonly System.Random rnd = new();

	private bool[] _indexUsed;
	private bool[] _vertexUsed;

	// ------------------------------------------------------------------------
	// INSPECTOR SETTINGS
	// ------------------------------------------------------------------------
	[Header("Allocator Limits (4–1024)")]
	[Range(4, 1024)] public int maxIndexBlocks = 64;
	[Range(4, 1024)] public int maxVertexBlocks = 64;

	[Header("Falling Speed")]
	[Range(1f, 10f)] public float fallDurationRange = 2f;

	[Header("Strip Width")]
	[Range(10f, 100f)] public float stripWidth = 60f;

	[Header("Spawning")]
	[Range(0.01f, 1f)] public float spawnDelay = 0.04f;

	[Header("Material")]
	public Material stripMaterial;

	// ------------------------------------------------------------------------
	// Layout
	// ------------------------------------------------------------------------
	private const int largeCell = 22;
	private const int pad = 30;
	private const int largeGridW = 16 * largeCell;
	private int panelX;
	private const int panelW = 300;

	private readonly List<FallingStrip> fallingStrips = new();
	private readonly List<FallingStrip> stripsToRelease = new();

	private struct FallingStrip
	{
		public QuadStrip strip;
		public float startTime;
		public float xOffset;
		public float fallSpeed;
	}

	// ------------------------------------------------------------------------
	// Unity callbacks
	// ------------------------------------------------------------------------
	private void Awake()
	{
		// --- Camera (orthographic, pixel-perfect) ---
		cam = Camera.main ?? gameObject.AddComponent<Camera>();
		cam.tag = "MainCamera";
		cam.orthographic = true;
		cam.orthographicSize = Screen.height * 0.5f;
		cam.transform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, -10f);
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

		// --- Layout ---
		int infoX = pad + largeGridW + 30;
		panelX = infoX + 200;

		// --- Mesh ---
		meshFilter = GetComponent<MeshFilter>();
		meshRenderer = GetComponent<MeshRenderer>();

		mesh = new Mesh { name = "QuadStripMesh_PixelSpace" };
		mesh.MarkDynamic();
		meshFilter.mesh = mesh;

		if (stripMaterial == null)
		{
			stripMaterial = new Material(Shader.Find("Unlit/Color"));
			stripMaterial.color = Color.white;
		}
		meshRenderer.material = stripMaterial;
		meshRenderer.enabled = true;

		// --- Allocator ---
		CreateAllocatorIfNeeded();
		ResizeDebugArrays();
		ClearDebugArrays();
	}

	private void CreateAllocatorIfNeeded()
	{
		if (quadAllocator != null) return;
		int idx = Mathf.Clamp(maxIndexBlocks, 4, 1024);
		int vtx = Mathf.Clamp(maxVertexBlocks, 4, 1024);
		quadAllocator = new QuadStripAllocator();
		quadAllocator.SetMaxIndexBlocks(idx);
		quadAllocator.SetMaxVertexBlocks(vtx);
	}

	private void Update()
	{
		if (quadAllocator == null) return;
		int idx = Mathf.Clamp(maxIndexBlocks, 4, 1024);
		int vtx = Mathf.Clamp(maxVertexBlocks, 4, 1024);
		if (quadAllocator.MaxIndexBlocks != idx) quadAllocator.SetMaxIndexBlocks(idx);
		if (quadAllocator.MaxVertexBlocks != vtx) quadAllocator.SetMaxVertexBlocks(vtx);
	}

	private void Start() => StartCoroutine(SpawnRoutine());

	// ------------------------------------------------------------------------
	// Spawning
	// ------------------------------------------------------------------------
	private IEnumerator SpawnRoutine()
	{
		while (true)
		{
			int numQuads = 1 + rnd.Next(8);
			var strip = quadAllocator.AllocateStrip(numQuads);
			if (strip == null) { yield return null; continue; }

			foreach (int i in strip.indexBlocks) if (i < _indexUsed.Length) _indexUsed[i] = true;
			foreach (int v in strip.vertexBlocks) if (v < _vertexUsed.Length) _vertexUsed[v] = true;

			float xOffset = ((float)rnd.NextDouble() * 2f - 1f);
			float duration = Random.Range(1f, fallDurationRange);
			float fallSpeed = Screen.height / duration;

			var falling = new FallingStrip
			{
				strip = strip,
				startTime = Time.time,
				xOffset = xOffset,
				fallSpeed = fallSpeed
			};

			var colors = quadAllocator.MutableColors;
			var uv = quadAllocator.MutableUV;
			for (int i = 0; i <= numQuads; i++)
			{
				int vIdx = strip.vertexBlocks[i] * QuadStripAllocator.VerticesPerBlock;
				if (vIdx + 1 >= colors.Count) break;
				colors[vIdx] = colors[vIdx + 1] = Color.white;
				float v = i * 0.125f;
				uv[vIdx] = new Vector2(0f, v);
				uv[vIdx + 1] = new Vector2(1f, v);
			}

			fallingStrips.Add(falling);
			yield return new WaitForSeconds(spawnDelay);
		}
	}

	// ------------------------------------------------------------------------
	// Animation & Mesh Update (PIXEL SPACE) – **FALLING DOWN**
	// ------------------------------------------------------------------------
	private void LateUpdate()
	{
		UpdateFallingStrips();
		UpdateMeshBuffers();
	}

	private void UpdateFallingStrips()
	{
		float now = Time.time;
		stripsToRelease.Clear();
		var vertices = quadAllocator.MutableVertices;

		for (int i = 0; i < fallingStrips.Count; i++)
		{
			var f = fallingStrips[i];
			if (f.strip == null) continue;

			int numQuads = f.strip.indexBlocks.Count;
			float elapsed = now - f.startTime;
			float fallDistance = f.fallSpeed * elapsed;
			float stripHeight = numQuads * stripWidth;

			// Full intended position (ignoring panel bounds)
			float yBottom = Screen.height - fallDistance;
			float yTop = yBottom + stripHeight;

			if (yTop < 0)
			{
				// Fully below → release
				stripsToRelease.Add(f);
				continue;
			}

			// --- X Position ---
			float maxX = panelX + panelW - stripWidth;
			float t = (f.xOffset + 1f) * 0.5f;
			float xLeft = Mathf.Lerp(panelX, maxX, t);
			float xRight = xLeft + stripWidth;

			// --- Write vertices ---
			for (int j = 0; j <= numQuads; j++)
			{
				float tY = (float)j / numQuads;
				float pixelY = Mathf.Lerp(yBottom, yTop, tY);

				int vBlock = f.strip.vertexBlocks[j];
				int vIdx = vBlock * QuadStripAllocator.VerticesPerBlock;
				if (vIdx + 1 < vertices.Count)
				{
					vertices[vIdx] = new Vector3(xLeft, pixelY, 0f);
					vertices[vIdx + 1] = new Vector3(xRight, pixelY, 0f);
				}
			}

			fallingStrips[i] = f;
		}

		// --- Release ---
		foreach (var f in stripsToRelease)
		{
			foreach (int idx in f.strip.indexBlocks) if (idx < _indexUsed.Length) _indexUsed[idx] = false;
			foreach (int vtx in f.strip.vertexBlocks) if (vtx < _vertexUsed.Length) _vertexUsed[vtx] = false;
			quadAllocator.ReleaseStrip(f.strip);
			fallingStrips.Remove(f);
		}

		// --- Defrag (light) ---
		quadAllocator.Defrag();
	}

	private void UpdateMeshBuffers()
	{
		mesh.Clear();
		mesh.SetVertices(quadAllocator.MutableVertices);
		mesh.SetColors(quadAllocator.MutableColors);
		mesh.SetUVs(0, quadAllocator.MutableUV);
		mesh.SetTriangles(quadAllocator.MutableIndices, 0);
		mesh.RecalculateBounds();
	}

	// ------------------------------------------------------------------------
	// OnGUI – heatmaps + stats + panel + wireframe
	// ------------------------------------------------------------------------
	private void OnGUI()
	{
		const int GRID_SIZE = 32;
		const int CELL_SIZE = 8;
		const int MAP_SIZE = GRID_SIZE * CELL_SIZE;
		const int GAP = 20;
		int left = pad + (largeGridW - MAP_SIZE) / 2;

		DrawHeatmap(_indexUsed, left, pad, "Index Blocks", new Color(1f, 0.6f, 0f));
		DrawHeatmap(_vertexUsed, left, pad + MAP_SIZE + GAP, "Vertex Blocks", new Color(0.3f, 1f, 0.3f));

		GUI.color = Color.white;
		int infoX = pad + largeGridW;
		GUILayout.BeginArea(new Rect(infoX, pad, 400, 500));
		{
			GUILayout.Label("<b>QuadStrip Allocator Test</b>", Rich());
			GUILayout.Space(8);
			GUILayout.Label($"Active Strips: <color=yellow>{fallingStrips.Count}</color>");
			GUILayout.Label($"Index Blocks: <color=orange>{quadAllocator.IndexBlockAllocated}</color>/{quadAllocator.IndexHighWater}");
			GUILayout.Label($"Vertex Blocks: <color=lime>{quadAllocator.VertexBlockAllocated}</color>/{quadAllocator.VertexHighWater}");
		}
		GUILayout.EndArea();
	}

	// ------------------------------------------------------------------------
	// Helpers
	// ------------------------------------------------------------------------
	private void ResizeDebugArrays()
	{
		const int max = 1024;
		if (_indexUsed == null || _indexUsed.Length < max) _indexUsed = new bool[max];
		if (_vertexUsed == null || _vertexUsed.Length < max) _vertexUsed = new bool[max];
	}

	private void ClearDebugArrays()
	{
		for (int i = 0; i < _indexUsed.Length; i++) _indexUsed[i] = false;
		for (int i = 0; i < _vertexUsed.Length; i++) _vertexUsed[i] = false;
	}

	private void DrawHeatmap(bool[] used, int x, int y, string title, Color usedColor)
	{
		const int GRID_SIZE = 32;
		const int CELL_SIZE = 8;
		const int INNER = CELL_SIZE - 1;

		for (int gy = 0; gy < GRID_SIZE; gy++)
			for (int gx = 0; gx < GRID_SIZE; gx++)
			{
				int id = gy * GRID_SIZE + gx;
				bool active = id < used.Length && used[id];
				GUI.color = active ? usedColor : Color.gray;
				GUI.DrawTexture(new Rect(x + gx * CELL_SIZE, y + gy * CELL_SIZE, INNER, INNER), Texture2D.whiteTexture);
			}

		GUI.color = Color.white;
		GUI.Label(new Rect(x, y - 20, 300, 20), $"<b>{title}</b>", Rich());
	}

	private GUIStyle Rich() => new GUIStyle(GUI.skin.label) { richText = true };

	private void OnDestroy()
	{
		if (mesh) Destroy(mesh);
	}
}