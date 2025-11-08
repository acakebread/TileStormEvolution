using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class QuadStripAllocatorTest : MonoBehaviour
{
	private QuadStripAllocator quadAllocator = new();
	private readonly System.Random rnd = new();

	[Header("Falling Speed (px/sec)")]
	[Range(1f, 10f)]
	[SerializeField] private float fallDurationRange = 2f;

	[Header("Strip Width (px)")]
	[Range(10f, 100f)]
	[SerializeField] private float stripWidth = 60f;

	[Header("Spawning")]
	[Range(0.01f, 1f)]
	[SerializeField] private float spawnDelay = 0.04f;

	[Header("Material")]
	[SerializeField] private Material stripMaterial;

	[Header("Debug")]
	[SerializeField] private bool wireframe = true;

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

	private void Awake()
	{
		quadAllocator.Initialise();

		int infoX = pad + largeGridW + 30;
		panelX = infoX + 200;
		panelY = pad;

		if (stripMaterial == null)
			stripMaterial = new Material(Shader.Find("Unlit/Color"));

		if (stripWidth < 10f) stripWidth = 60f;
	}

	private void Start() => StartCoroutine(SpawnRoutine());

	private IEnumerator SpawnRoutine()
	{
		while (true)
		{
			int numQuads = 1 + rnd.Next(6);
			var strip = quadAllocator.AllocateStrip(numQuads);
			if (strip != null)
			{
				float xOffset = ((float)rnd.NextDouble() * 2f - 1f);
				float duration = Random.Range(1f, fallDurationRange);
				float _fallSpeed = panelH / duration;

				var falling = new FallingStrip
				{
					strip = strip,
					startTime = Time.time,
					xOffset = xOffset,
					fallSpeed = _fallSpeed
				};

				// SET COLOR + UVs
				var va = quadAllocator.VertexAllocator;

				for (int i = 0; i <= numQuads; i++)
				{
					int vBlock = strip.vertexBlocks[i];
					int vIdx = vBlock * 2;

					va.colors[vIdx] = Color.white;
					va.colors[vIdx + 1] = Color.white;

					float v = (float)i / numQuads;
					va.uv[vIdx] = new Vector2(0f, v);  // left
					va.uv[vIdx + 1] = new Vector2(1f, v);  // right
				}

				fallingStrips.Add(falling);
			}
			yield return new WaitForSeconds(spawnDelay);
		}
	}

	private void LateUpdate()
	{
		UpdateFallingMesh();
	}

	private void UpdateFallingMesh()
	{
		float now = Time.time;
		stripsToRelease.Clear();

		// USE INDEXED LOOP TO ALLOW MODIFICATION
		for (int i = 0; i < fallingStrips.Count; i++)
		{
			var falling = fallingStrips[i];
			var strip = falling.strip;
			if (strip == null) continue;

			int numQuads = strip.indexBlocks.Count;

			float elapsed = now - falling.startTime;
			float fallDistance = falling.fallSpeed * elapsed;
			float stripHeight = numQuads * stripWidth;
			float yBottom = panelY + fallDistance;
			float yTop = yBottom - stripHeight;

			if (yTop >= panelY + panelH)
			{
				stripsToRelease.Add(falling);
				continue;
			}

			yTop = Mathf.Max(yTop, panelY);
			yBottom = Mathf.Min(yBottom, panelY + panelH);

			float maxX = panelX + panelW - stripWidth;
			float t = (falling.xOffset + 1f) * 0.5f;
			float xLeft = Mathf.Lerp(panelX, maxX, t);
			float xRight = xLeft + stripWidth;

			// UPDATE VERTICES
			for (int j = 0; j <= numQuads; j++)
			{
				float quadT = (float)j / numQuads;
				float y = Mathf.Lerp(yTop, yBottom, quadT);

				int vBlock = strip.vertexBlocks[j];
				int vIdx = vBlock * 2;

				var va = quadAllocator.VertexAllocator;
				va.vertices[vIdx] = new Vector3(xLeft, y, 0);
				va.vertices[vIdx + 1] = new Vector3(xRight, y, 0);
			}

			// UPDATE TEMP FIELDS
			falling.tempXLeft = xLeft;
			falling.tempXRight = xRight;
			falling.tempYTop = yTop;
			falling.tempYBottom = yBottom;

			// WRITE BACK TO LIST
			fallingStrips[i] = falling;
		}

		// RELEASE AFTER LOOP
		foreach (var falling in stripsToRelease)
		{
			quadAllocator.ReleaseStrip(falling.strip);
			fallingStrips.Remove(falling);
		}
	}

	private void OnGUI()
	{
		int y = pad;

		// --------------------------------------------------------------
		// 1. Two 32×32 heatmaps — SAME ORDER AS ORIGINAL (gy=0 = bottom row)
		// --------------------------------------------------------------
		const int GRID_SIZE = 32;
		const int GAP = 30;
		const int AVAILABLE_HEIGHT = panelH - GAP;
		const int MAP_HEIGHT = AVAILABLE_HEIGHT / 2;

		const int CELL_SIZE = MAP_HEIGHT / GRID_SIZE;     // ~8 px
		const int MAP_PIXEL_SIZE = GRID_SIZE * CELL_SIZE;

		int leftOffset = pad + (largeGridW - MAP_PIXEL_SIZE) / 2;

		int topY = y;
		int bottomY = topY + MAP_PIXEL_SIZE + GAP;

		DrawAllocatorHeatmap(
			quadAllocator.IndexAllocator,
			topY,
			leftOffset,
			CELL_SIZE,
			GRID_SIZE,
			"Index Blocks",
			new Color(1f, 0.6f, 0f, 1f));

		DrawAllocatorHeatmap(
			quadAllocator.VertexAllocator,
			bottomY,
			leftOffset,
			CELL_SIZE,
			GRID_SIZE,
			"Vertex Blocks",
			new Color(0.3f, 1f, 0.3f, 1f));

		// --------------------------------------------------------------
		// 2. Info panel
		// --------------------------------------------------------------
		GUI.color = Color.white;
		int infoX = pad + largeGridW + 30;
		GUILayout.BeginArea(new Rect(infoX, pad, 360, 500));
		{
			GUILayout.Label("<b>QuadStrip Allocator Test</b>", Rich());
			GUILayout.Space(8);
			GUILayout.Label($"Active Strips: <color=yellow>{fallingStrips.Count}</color>");
			GUILayout.Label($"Index Blocks: <color=orange>{quadAllocator.IndexAllocator.AllocatedBlockCount}</color>/{GRID_SIZE * GRID_SIZE}");
			GUILayout.Label($"Vertex Blocks: <color=lime>{quadAllocator.VertexAllocator.AllocatedBlockCount}</color>/{GRID_SIZE * GRID_SIZE}");
		}
		GUILayout.EndArea();

		// --------------------------------------------------------------
		// 3. Falling-strip panel (unchanged)
		// --------------------------------------------------------------
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

		// --------------------------------------------------------------
		// 4. Render the strips with GL (unchanged)
		// --------------------------------------------------------------
		if (Event.current.type == EventType.Repaint)
		{
			stripMaterial.SetPass(0);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

			GL.Begin(GL.TRIANGLES);
			var ia = quadAllocator.IndexAllocator;
			var va = quadAllocator.VertexAllocator;
			for (int i = 0; i < ia.indices.Length; i += 3)
			{
				int i0 = ia.indices[i];
				int i1 = ia.indices[i + 1];
				int i2 = ia.indices[i + 2];
				if (i0 >= va.vertices.Length || i1 >= va.vertices.Length || i2 >= va.vertices.Length) continue;

				Vector3 v0 = va.vertices[i0];
				Vector3 v1 = va.vertices[i1];
				Vector3 v2 = va.vertices[i2];
				Color c0 = va.colors[i0];
				Color c1 = va.colors[i1];
				Color c2 = va.colors[i2];
				Vector2 uv0 = va.uv[i0];
				Vector2 uv1 = va.uv[i1];
				Vector2 uv2 = va.uv[i2];

				GL.Color(c0); GL.TexCoord2(uv0.x, uv0.y); GL.Vertex(v0);
				GL.Color(c1); GL.TexCoord2(uv1.x, uv1.y); GL.Vertex(v1);
				GL.Color(c2); GL.TexCoord2(uv2.x, uv2.y); GL.Vertex(v2);
			}
			GL.End();

			if (wireframe)
			{
				GL.Begin(GL.LINES);
				GL.Color(Color.white);
				foreach (var falling in fallingStrips)
				{
					var strip = falling.strip;
					if (strip == null || falling.tempYTop <= 0) continue;

					float x1 = falling.tempXLeft;
					float x2 = falling.tempXRight;
					float y1 = falling.tempYTop;
					float y2 = falling.tempYBottom;
					int numQuads = strip.indexBlocks.Count;

					for (int i = 0; i <= numQuads; i++)
					{
						float _y = Mathf.Lerp(y1, y2, (float)i / numQuads);
						GL.Vertex3(x1, _y, 0);
						GL.Vertex3(x2, _y, 0);
					}
					GL.Vertex3(x1, y1, 0); GL.Vertex3(x1, y2, 0);
					GL.Vertex3(x2, y1, 0); GL.Vertex3(x2, y2, 0);
				}
				GL.End();
			}
			GL.PopMatrix();
		}
	}

	// ---------------------------------------------------------------------
	// NEW: generic heat-map drawer – cell size is passed in
	// ---------------------------------------------------------------------
	private void DrawAllocatorHeatmap(DynamicAllocator alloc,
									 int yStart,
									 int xStart,
									 int cellSize,
									 int gridSize,
									 string title,
									 Color usedCol)
	{
		int inner = cellSize - 1;

		for (int gy = 0; gy < gridSize; gy++)
		{
			for (int gx = 0; gx < gridSize; gx++)
			{
				bool isUsed = alloc.IsBlockAllocated(gy * 32 + gx);

				GUI.color = isUsed ? usedCol : Color.gray;

				Rect r = new Rect(
					xStart + gx * cellSize,
					yStart + gy * cellSize,
					inner,
					inner);

				GUI.DrawTexture(r, Texture2D.whiteTexture);
			}
		}

		GUI.color = Color.white;
		GUI.Label(new Rect(xStart, yStart - 20, 400, 20), $"<b>{title} ({gridSize}×{gridSize})</b>", Rich());
	}

	private GUIStyle Rich() => new GUIStyle(GUI.skin.label) { richText = true };
}