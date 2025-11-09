// QuadStripAllocatorTest.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class QuadStripAllocatorTest : MonoBehaviour
{
	private QuadStripAllocator quadAllocator;

	private readonly System.Random rnd = new();

	private bool[] _indexUsed;
	private bool[] _vertexUsed;

	// ------------------------------------------------------------------------
	// INSPECTOR SETTINGS – Adjust here (4–1024)
	// ------------------------------------------------------------------------
	[Header("Allocator Limits (4–1024)")]
	[Range(4, 1024)] public int maxIndexBlocks = 4;
	[Range(4, 1024)] public int maxVertexBlocks = 4;

	[Header("Falling Speed")]
	[Range(1f, 10f)] public float fallDurationRange = 2f;

	[Header("Strip Width")]
	[Range(10f, 100f)] public float stripWidth = 60f;

	[Header("Spawning")]
	[Range(0.01f, 1f)] public float spawnDelay = 0.04f;

	[Header("Material")]
	public Material stripMaterial;

	[Header("Debug")]
	public bool wireframe = true;

	// ------------------------------------------------------------------------
	// Layout
	// ------------------------------------------------------------------------
	private const int largeCell = 22;
	private const int pad = 30;
	private const int largeGridW = 16 * largeCell;
	private int panelX, panelY;
	private const int panelW = 300;
	private const int panelH = 600;

	private readonly List<FallingStrip> fallingStrips = new();
	private readonly List<FallingStrip> stripsToRelease = new();

	private struct FallingStrip
	{
		public QuadStrip strip;
		public float startTime;
		public float xOffset;
		public float fallSpeed;
		public float tempXLeft;
		public float tempXRight;
		public float tempYTop;
		public float tempYBottom;
	}

	// ------------------------------------------------------------------------
	// Unity callbacks
	// ------------------------------------------------------------------------
	private void Awake()
	{
		int infoX = pad + largeGridW + 30;
		panelX = infoX + 200;
		panelY = pad;

		if (stripMaterial == null)
			stripMaterial = new Material(Shader.Find("Unlit/Color"));

		CreateAllocatorIfNeeded();
		ResizeDebugArrays();
		ClearDebugArrays();
	}

	private void CreateAllocatorIfNeeded()
	{
		if (quadAllocator != null) return;

		int clampedIndex = Mathf.Clamp(maxIndexBlocks, 4, 1024);
		int clampedVertex = Mathf.Clamp(maxVertexBlocks, 4, 1024);
		quadAllocator = new QuadStripAllocator(clampedIndex, clampedVertex);
	}

	private void Update()
	{
		// LIVE UPDATE MAX BLOCKS — NO REBUILD!
		if (quadAllocator != null)
		{
			int clampedIndex = Mathf.Clamp(maxIndexBlocks, 4, 1024);
			int clampedVertex = Mathf.Clamp(maxVertexBlocks, 4, 1024);

			if (quadAllocator.MaxIndexBlocks != clampedIndex)
				quadAllocator.SetMaxIndexBlocks(clampedIndex);
			if (quadAllocator.MaxVertexBlocks != clampedVertex)
				quadAllocator.SetMaxVertexBlocks(clampedVertex);
		}
	}

	private void Start() => StartCoroutine(SpawnRoutine());

	// ------------------------------------------------------------------------
	// Spawning
	// ------------------------------------------------------------------------
	private IEnumerator SpawnRoutine()
	{
		while (true)
		{
			int numQuads = 1 + rnd.Next(6);
			var strip = quadAllocator.AllocateStrip(numQuads);
			if (strip != null)
			{
				foreach (int idx in strip.indexBlocks) if (idx < _indexUsed.Length) _indexUsed[idx] = true;
				foreach (int vtx in strip.vertexBlocks) if (vtx < _vertexUsed.Length) _vertexUsed[vtx] = true;

				float xOffset = ((float)rnd.NextDouble() * 2f - 1f);
				float duration = Random.Range(1f, fallDurationRange);
				float fallSpeed = panelH / duration;

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
					int vBlock = strip.vertexBlocks[i];
					int vIdx = vBlock * QuadStripAllocator.VerticesPerBlock;
					if (vIdx + 1 < colors.Count)
					{
						colors[vIdx] = colors[vIdx + 1] = Color.white;
						float v = (float)i / numQuads;
						uv[vIdx] = new Vector2(0f, v);
						uv[vIdx + 1] = new Vector2(1f, v);
					}
				}

				fallingStrips.Add(falling);
			}
			yield return new WaitForSeconds(spawnDelay);
		}
	}

	// ------------------------------------------------------------------------
	// Animation
	// ------------------------------------------------------------------------
	private void LateUpdate() => UpdateFallingMesh();

	private void UpdateFallingMesh()
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
			float yBottom = panelY + fallDistance;
			float yTop = yBottom - stripHeight;

			if (yTop >= panelY + panelH) { stripsToRelease.Add(f); continue; }

			yTop = Mathf.Max(yTop, panelY);
			yBottom = Mathf.Min(yBottom, panelY + panelH);

			float maxX = panelX + panelW - stripWidth;
			float t = (f.xOffset + 1f) * 0.5f;
			float xLeft = Mathf.Lerp(panelX, maxX, t);
			float xRight = xLeft + stripWidth;

			for (int j = 0; j <= numQuads; j++)
			{
				float posY = Mathf.Lerp(yTop, yBottom, (float)j / numQuads);
				int vBlock = f.strip.vertexBlocks[j];
				int vIdx = vBlock * QuadStripAllocator.VerticesPerBlock;
				if (vIdx + 1 < vertices.Count)
				{
					vertices[vIdx] = new Vector3(xLeft, posY, 0);
					vertices[vIdx + 1] = new Vector3(xRight, posY, 0);
				}
			}

			f.tempXLeft = xLeft; f.tempXRight = xRight;
			f.tempYTop = yTop; f.tempYBottom = yBottom;
			fallingStrips[i] = f;
		}

		foreach (var f in stripsToRelease)
		{
			foreach (int idx in f.strip.indexBlocks) if (idx < _indexUsed.Length) _indexUsed[idx] = false;
			foreach (int vtx in f.strip.vertexBlocks) if (vtx < _vertexUsed.Length) _vertexUsed[vtx] = false;
			quadAllocator.ReleaseStrip(f.strip);
			fallingStrips.Remove(f);
		}
	}

	// ------------------------------------------------------------------------
	// GUI
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
			GUILayout.Label($"Index Blocks: <color=orange>{quadAllocator.IndexBlockAllocated}</color>/{quadAllocator.MaxIndexBlocks} " +
						   $"(high: {quadAllocator.IndexHighWater})");
			GUILayout.Label($"Vertex Blocks: <color=lime>{quadAllocator.VertexBlockAllocated}</color>/{quadAllocator.MaxVertexBlocks} " +
						   $"(high: {quadAllocator.VertexHighWater})");
		}
		GUILayout.EndArea();

		GUI.color = new Color(0.05f, 0.05f, 0.1f, 1f);
		GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
		GUI.color = Color.white;

		GUI.Label(new Rect(panelX, panelY - 20, panelW, 20), "<b>Falling QuadStrips</b>", Rich());

		GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
		for (int i = 1; i < 5; i++)
		{
			float py = panelY + i * (panelH / 5f);
			GUI.DrawTexture(new Rect(panelX, py, panelW, 1), Texture2D.whiteTexture);
			float px = panelX + i * (panelW / 5f);
			GUI.DrawTexture(new Rect(px, panelY, 1, panelH), Texture2D.whiteTexture);
		}
		GUI.color = Color.white;

		if (Event.current.type == EventType.Repaint)
		{
			stripMaterial.SetPass(0);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

			var indices = quadAllocator.Indices;
			var vertices = quadAllocator.Vertices;
			var colors = quadAllocator.Colors;
			var uv = quadAllocator.UV;

			GL.Begin(GL.TRIANGLES);
			for (int i = 0; i < indices.Count; i += 3)
			{
				int i0 = indices[i];
				int i1 = indices[i + 1];
				int i2 = indices[i + 2];
				if (i0 >= vertices.Count || i1 >= vertices.Count || i2 >= vertices.Count) continue;

				Vector3 v0 = vertices[i0]; Vector3 v1 = vertices[i1]; Vector3 v2 = vertices[i2];
				Color c0 = colors[i0]; Color c1 = colors[i1]; Color c2 = colors[i2];
				Vector2 u0 = uv[i0]; Vector2 u1 = uv[i1]; Vector2 u2 = uv[i2];

				GL.Color(c0); GL.TexCoord2(u0.x, u0.y); GL.Vertex(v0);
				GL.Color(c1); GL.TexCoord2(u1.x, u1.y); GL.Vertex(v1);
				GL.Color(c2); GL.TexCoord2(u2.x, u2.y); GL.Vertex(v2);
			}
			GL.End();

			if (wireframe)
			{
				GL.Begin(GL.LINES);
				GL.Color(Color.white);
				foreach (var f in fallingStrips)
				{
					if (f.strip == null) continue;
					float x1 = f.tempXLeft, x2 = f.tempXRight, y1 = f.tempYTop, y2 = f.tempYBottom;
					int n = f.strip.indexBlocks.Count;
					for (int i = 0; i <= n; i++)
					{
						float y = Mathf.Lerp(y1, y2, (float)i / n);
						GL.Vertex3(x1, y, 0);
						GL.Vertex3(x2, y, 0);
					}
					GL.Vertex3(x1, y1, 0); GL.Vertex3(x1, y2, 0);
					GL.Vertex3(x2, y1, 0); GL.Vertex3(x2, y2, 0);
				}
				GL.End();
			}

			GL.PopMatrix();
		}
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
}