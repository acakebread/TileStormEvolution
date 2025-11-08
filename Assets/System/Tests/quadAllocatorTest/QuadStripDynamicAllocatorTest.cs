using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(QuadStripAllocator))]
public class QuadStripDynamicAllocatorTest : MonoBehaviour
{
	private QuadStripAllocator quadAllocator;
	private readonly System.Random rnd = new();

	[Header("Falling Speed (px/sec)")]
	[SerializeField] private float baseFallSpeed = 200f;
	[SerializeField] private float fallSpeedBonus = 600f;

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

	// === LAYOUT CONSTANTS ===
	private const int pad = 30;
	private const int spacing = 40;
	private const int largeCell = 22;
	private const int smallCell = 11;
	private const int largeGridW = 16 * largeCell;  // 352
	private const int smallGridW = 16 * smallCell;  // 176
	private const int panelW = 300;
	private const int panelH = 600;  // ORIGINAL SIZE

	private int panelX, panelY;

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
		quadAllocator = GetComponent<QuadStripAllocator>();

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
				float duration = panelH / (baseFallSpeed + fallSpeedBonus / (numQuads + 1));
				float fallSpeed = panelH / duration;

				var falling = new FallingStrip
				{
					strip = strip,
					startTime = Time.time,
					xOffset = xOffset,
					fallSpeed = fallSpeed
				};

				var va = quadAllocator.VertexAllocator;
				for (int i = 0; i <= numQuads; i++)
				{
					int vBlock = strip.vertexBlocks[i];
					int vIdx = vBlock * 2;

					va.colors[vIdx] = va.colors[vIdx + 1] = Color.white;

					float v = (float)i / numQuads;
					va.uv[vIdx] = new Vector2(0f, v);
					va.uv[vIdx + 1] = new Vector2(1f, v);
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

			falling.tempXLeft = xLeft;
			falling.tempXRight = xRight;
			falling.tempYTop = yTop;
			falling.tempYBottom = yBottom;
			fallingStrips[i] = falling;
		}

		foreach (var falling in stripsToRelease)
		{
			quadAllocator.ReleaseStrip(falling.strip);
			fallingStrips.Remove(falling);
		}
	}

	private void OnGUI()
	{
		GUI.skin.label.fontSize = 12;
		GUI.color = Color.white;

		// === TITLE: TOP CENTER ===
		GUI.Label(new Rect(Screen.width * 0.5f - 180, 8, 360, 30),
			"<b>QuadStrip Dynamic Allocator Test</b>", RichCenter());

		// === LAYOUT BASED ON TITLE ===
		int topY = 8 + 30 + 20;  // Title + height + padding
		int heatmapX = pad;
		int heatmapY = topY;
		int heatmapW = largeGridW;

		// === QUAD USAGE HEATMAP ===
		DrawQuadGrid(heatmapX, heatmapY);

		// === BARS: EDGES MATCH HEATMAP, GAP BETWEEN THEM ===
		int barsY = heatmapY + heatmapW + spacing;
		int barH = 20;
		int barW = smallGridW;
		int gapBetweenBars = 30;

		int indexBarX = heatmapX;                                   // LEFT = HEATMAP LEFT
		int vertexBarX = heatmapX + heatmapW - barW;                // RIGHT = HEATMAP RIGHT

		DrawUsageBar(new Rect(indexBarX, barsY, barW - gapBetweenBars * 0.5f, barH),
			quadAllocator.IndexAllocator.AllocatedBlockCount,
			DynamicAllocator.Blocks,
			"Index Blocks",
			new Color(1f, 0.6f, 0f, 1f));

		DrawUsageBar(new Rect(vertexBarX + gapBetweenBars * 0.5f, barsY, barW - gapBetweenBars * 0.5f, barH),
			quadAllocator.VertexAllocator.AllocatedBlockCount,
			DynamicAllocator.Blocks,
			"Vertex Blocks",
			new Color(0.3f, 1f, 0.3f, 1f));

		// === FALLING PANEL: BELOW TITLE, ORIGINAL SIZE, RIGHT SIDE ===
		int panelTopY = topY;
		int panelLeftX = heatmapX + heatmapW + 30 + 200;  // Original spacing
		GUI.color = new Color(0.05f, 0.05f, 0.1f, 1f);
		GUI.DrawTexture(new Rect(panelLeftX, panelTopY, panelW, panelH), Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUI.Label(new Rect(panelLeftX, panelTopY - 20, panelW, 20), "<b>Falling QuadStrips</b>", Rich());

		// Store for rendering
		panelX = panelLeftX;
		panelY = panelTopY;

		// Grid lines
		GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
		for (int i = 1; i < 5; i++)
		{
			float py = panelTopY + i * (panelH / 5f);
			GUI.DrawTexture(new Rect(panelLeftX, py, panelW, 1), Texture2D.whiteTexture);
			float px = panelLeftX + i * (panelW / 5f);
			GUI.DrawTexture(new Rect(px, panelTopY, 1, panelH), Texture2D.whiteTexture);
		}
		GUI.color = Color.white;

		// === RENDERING ===
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
				int i0 = ia.indices[i], i1 = ia.indices[i + 1], i2 = ia.indices[i + 2];
				if (i0 >= va.vertices.Length || i1 >= va.vertices.Length || i2 >= va.vertices.Length) continue;

				Vector3 v0 = va.vertices[i0]; Vector3 v1 = va.vertices[i1]; Vector3 v2 = va.vertices[i2];
				Color c0 = va.colors[i0]; Color c1 = va.colors[i1]; Color c2 = va.colors[i2];
				Vector2 uv0 = va.uv[i0]; Vector2 uv1 = va.uv[i1]; Vector2 uv2 = va.uv[i2];

				GL.Color(c0); GL.TexCoord(uv0); GL.Vertex(v0);
				GL.Color(c1); GL.TexCoord(uv1); GL.Vertex(v1);
				GL.Color(c2); GL.TexCoord(uv2); GL.Vertex(v2);
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

					float x1 = falling.tempXLeft, x2 = falling.tempXRight;
					float y1 = falling.tempYTop, y2 = falling.tempYBottom;
					int n = strip.indexBlocks.Count;

					for (int i = 0; i <= n; i++)
					{
						float _y = Mathf.Lerp(y1, y2, (float)i / n);
						GL.Vertex3(x1, _y, 0); GL.Vertex3(x2, _y, 0);
					}
					GL.Vertex3(x1, y1, 0); GL.Vertex3(x1, y2, 0);
					GL.Vertex3(x2, y1, 0); GL.Vertex3(x2, y2, 0);
				}
				GL.End();
			}
			GL.PopMatrix();
		}
	}

	private void DrawQuadGrid(int x, int y)
	{
		for (int gy = 0; gy < 16; gy++)
			for (int gx = 0; gx < 16; gx++)
			{
				int id = gy * 16 + gx;
				Rect r = new Rect(x + gx * largeCell, y + gy * largeCell, largeCell - 2, largeCell - 2);
				bool used = false;

				for (int i = 0; i < fallingStrips.Count; i++)
				{
					var strip = fallingStrips[i].strip;
					if (strip != null && strip.indexBlocks.Contains(id))
					{
						used = true;
						break;
					}
				}

				GUI.color = used ? Color.cyan : Color.red;
				GUI.DrawTexture(r, Texture2D.whiteTexture);
			}

		GUI.color = Color.white;
		GUI.Label(new Rect(x, y - 20, largeGridW, 20),
			$"<b>Quad Usage</b>  |  Active: <color=yellow>{fallingStrips.Count}</color>", Rich());
	}

	private void DrawUsageBar(Rect rect, int used, int total, string label, Color fillColor)
	{
		// Text, Title + numbers
		GUI.color = Color.white;
		string txt = $"<b>{label}</b>\n<color=white>{used}</color>/<color=#FFFF88>{total}</color>";
		GUI.Label(new Rect(rect.x, rect.y - 36, rect.width, 40), txt, Rich());

		// Background
		GUI.color = new Color(0.15f, 0.15f, 0.15f, 1f);
		GUI.DrawTexture(rect, Texture2D.whiteTexture);

		// Fill
		float fill = Mathf.Clamp01((float)used / total);
		Rect fillRect = new Rect(rect.x, rect.y, rect.width * fill, rect.height);
		GUI.color = fillColor;
		GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
	}

	private GUIStyle Rich() => new GUIStyle(GUI.skin.label) { richText = true };
	private GUIStyle RichCenter() => new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter };
}