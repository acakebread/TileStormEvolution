using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(QuadStripAllocator))]
public class QuadStripAllocatorTest : MonoBehaviour
{
	private QuadStripAllocator quadAllocator;
	private readonly System.Random rnd = new();

	[Header("Falling Speed (px/sec)")]
	[SerializeField] private float baseFallSpeed = 200f;
	[SerializeField] private float fallSpeedBonus = 600f;

	[Header("Strip Width (px)")]
	[Range(10f, 100f)]
	[SerializeField] private float stripWidth = 60f;

	[Header("Material")]
	[SerializeField] private Material stripMaterial;

	[Header("Debug")]
	[SerializeField] private bool wireframe = true;

	private const int largeCell = 22;
	private const int smallCell = 11;
	private const int pad = 30;
	private const int spacing = 30;
	private const int largeGridW = 16 * largeCell;
	private const int smallGridW = 16 * smallCell;

	private int panelX, panelY;
	private const int panelW = 300;
	private const int panelH = 600;

	private readonly List<QuadStrip> stripsToRelease = new();

	private void Awake()
	{
		quadAllocator = GetComponent<QuadStripAllocator>();

		int infoX = pad + largeGridW + 30;
		panelX = infoX + 200;
		panelY = pad;

		if (stripMaterial == null)
			stripMaterial = new Material(Shader.Find("Unlit/Color"));

		if (stripWidth < 10f) stripWidth = 60f;

		//// TEST TEXTURE (optional)
		//Texture2D testTex = new Texture2D(2, 2);
		//testTex.SetPixel(0, 0, Color.red); testTex.SetPixel(1, 0, Color.green);
		//testTex.SetPixel(0, 1, Color.blue); testTex.SetPixel(1, 1, Color.yellow);
		//testTex.Apply();
		//stripMaterial.mainTexture = testTex;
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

				strip.startTime = Time.time;
				strip.xOffset = xOffset;
				strip.fallSpeed = fallSpeed;

				// SET COLOR + UVs
				Color cyan = new Color(0f, 1f, 1f);
				var va = quadAllocator.vertexAllocator;

				for (int i = 0; i <= strip.numQuads; i++)
				{
					int vBlock = strip.vertexBlocks[i];
					int vIdx = vBlock * 2;

					// COLOR
					va.colors[vIdx] = Color.white;// cyan;
					va.colors[vIdx + 1] = Color.white;//cyan;

					// UVs: U = 0 (left), 1 (right)
					// V = i / numQuads (top to bottom)
					float v = (float)i / strip.numQuads;
					va.uv[vIdx] = new Vector2(0f, v);  // left
					va.uv[vIdx + 1] = new Vector2(1f, v);  // right
				}
			}
			yield return new WaitForSeconds(0.04f);
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

		foreach (var strip in quadAllocator.ActiveStrips)
		{
			if (strip == null) continue;

			float elapsed = now - strip.startTime;
			float fallDistance = strip.fallSpeed * elapsed;
			float stripHeight = strip.numQuads * stripWidth;
			float yBottom = panelY + fallDistance;
			float yTop = yBottom - stripHeight;

			if (yTop >= panelY + panelH)
			{
				stripsToRelease.Add(strip);
				continue;
			}

			yTop = Mathf.Max(yTop, panelY);
			yBottom = Mathf.Min(yBottom, panelY + panelH);

			float maxX = panelX + panelW - stripWidth;
			float t = (strip.xOffset + 1f) * 0.5f;
			float xLeft = Mathf.Lerp(panelX, maxX, t);
			float xRight = xLeft + stripWidth;

			// UPDATE VERTICES ONLY
			for (int i = 0; i <= strip.numQuads; i++)
			{
				float quadT = (float)i / strip.numQuads;
				float y = Mathf.Lerp(yTop, yBottom, quadT);

				int vBlock = strip.vertexBlocks[i];
				int vIdx = vBlock * 2;

				var va = quadAllocator.vertexAllocator;
				va.vertices[vIdx] = new Vector3(xLeft, y, 0);
				va.vertices[vIdx + 1] = new Vector3(xRight, y, 0);
			}

			// STORE FOR OnGUI
			strip.tempXLeft = xLeft;
			strip.tempXRight = xRight;
			strip.tempYTop = yTop;
			strip.tempYBottom = yBottom;
		}

		foreach (var strip in stripsToRelease)
		{
			quadAllocator.ReleaseStrip(strip);
		}
	}

	private void OnGUI()
	{
		// === GUI LAYOUT ===
		int y = pad;
		DrawQuadGrid(y);
		y += largeGridW + spacing;

		int totalSmallWidth = 2 * smallGridW + 30;
		int leftOffset = (largeGridW - totalSmallWidth) / 2 + pad;

		DrawAllocatorGrid(quadAllocator.IndexAllocator, y, leftOffset, "Index Blocks", new Color(1f, 0.6f, 0f, 1f));
		DrawAllocatorGrid(quadAllocator.VertexAllocator, y, leftOffset + smallGridW + 30, "Vertex Blocks", new Color(0.3f, 1f, 0.3f, 1f));

		GUI.color = Color.white;
		int infoX = pad + largeGridW + 30;
		GUILayout.BeginArea(new Rect(infoX, pad, 360, 500));
		{
			GUILayout.Label("<b>QuadStrip Allocator Test</b>", Rich());
			GUILayout.Space(8);
			GUILayout.Label($"Active Strips: <color=yellow>{quadAllocator.ActiveStripCount}</color>");
			GUILayout.Label($"Index Blocks: <color=orange>{quadAllocator.IndexAllocator.AllocatedBlockCount}</color>/256");
			GUILayout.Label($"Vertex Blocks: <color=lime>{quadAllocator.VertexAllocator.AllocatedBlockCount}</color>/256");
		}
		GUILayout.EndArea();

		// === PANEL BACKGROUND ===
		GUI.color = new Color(0.05f, 0.05f, 0.1f, 1f);
		GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUI.Label(new Rect(panelX, panelY - 20, panelW, 20), "<b>Falling QuadStrips</b>", Rich());

		// === GRID LINES ===
		GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
		for (int i = 1; i < 5; i++)
		{
			float py = panelY + i * (panelH / 5f);
			GUI.DrawTexture(new Rect(panelX, py, panelW, 1), Texture2D.whiteTexture);
			float px = panelX + i * (panelW / 5f);
			GUI.DrawTexture(new Rect(px, panelY, 1, panelH), Texture2D.whiteTexture);
		}
		GUI.color = Color.white;

		// === RENDERING: SOLID + WIREFRAME ===
		if (Event.current.type == EventType.Repaint)
		{
			stripMaterial.SetPass(0);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

			// 1. SOLID QUADS (WITH UVs)
			GL.Begin(GL.TRIANGLES);
			var ia = quadAllocator.indexAllocator;
			var va = quadAllocator.vertexAllocator;
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

				GL.Color(c0); GL.TexCoord(uv0); GL.Vertex(v0);
				GL.Color(c1); GL.TexCoord(uv1); GL.Vertex(v1);
				GL.Color(c2); GL.TexCoord(uv2); GL.Vertex(v2);
			}
			GL.End();

			if (wireframe)
			{

				// 2. WHITE WIREFRAME
				GL.Begin(GL.LINES);
				GL.Color(Color.white);

				foreach (var strip in quadAllocator.ActiveStrips)
				{
					if (strip == null) continue;
					if (strip.tempYTop <= 0) continue; // skip uninitialized

					float x1 = strip.tempXLeft;
					float x2 = strip.tempXRight;
					float y1 = strip.tempYTop;
					float y2 = strip.tempYBottom;

					for (int i = 0; i <= strip.numQuads; i++)
					{
						float _y = Mathf.Lerp(y1, y2, (float)i / strip.numQuads);
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

	private void DrawQuadGrid(int yStart)
	{
		for (int gy = 0; gy < 16; gy++)
			for (int gx = 0; gx < 16; gx++)
			{
				int id = gy * 16 + gx;
				Rect r = new Rect(pad + gx * largeCell, yStart + gy * largeCell, largeCell - 2, largeCell - 2);
				bool used = false;
				foreach (var strip in quadAllocator.ActiveStrips)
				{
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
		GUI.Label(new Rect(pad, yStart - 20, 300, 20), "<b>Quad Usage</b>", Rich());
	}

	private void DrawAllocatorGrid(DynamicAllocator alloc, int yStart, int xStart, string title, Color usedCol)
	{
		for (int gy = 0; gy < 16; gy++)
			for (int gx = 0; gx < 16; gx++)
			{
				Rect r = new Rect(xStart + gx * smallCell, yStart + gy * smallCell, smallCell - 1, smallCell - 1);
				GUI.color = alloc.IsBlockAllocated(gx, gy) ? usedCol : Color.gray;
				GUI.DrawTexture(r, Texture2D.whiteTexture);
			}
		GUI.color = Color.white;
		GUI.Label(new Rect(xStart, yStart - 20, 200, 20), $"<b>{title}</b>", Rich());
	}

	private GUIStyle Rich() => new GUIStyle(GUI.skin.label) { richText = true };
}