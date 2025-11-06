using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(QuadStripAllocator))]
public class QuadStripAllocatorTest : MonoBehaviour
{
	private QuadStripAllocator quadAllocator;

	private readonly System.Random rnd = new System.Random();
	private readonly Dictionary<QuadStrip, (float start, float duration)> activeStrips = new();

	private const int largeCell = 22;
	private const int smallCell = 11;
	private const int pad = 60;
	private const int spacing = 30;
	private const int largeGridW = 16 * largeCell;
	private const int smallGridW = 16 * smallCell;

	private void Awake()
	{
		quadAllocator = GetComponent<QuadStripAllocator>();
	}

	private void Start() => StartCoroutine(TestRoutine());

	private IEnumerator TestRoutine()
	{
		while (true)
		{
			int numQuads = 1 + rnd.Next(6);
			var strip = quadAllocator.AllocateStrip(numQuads);
			if (strip != null)
			{
				float hold = 1.5f + (float)rnd.NextDouble() * 3.5f;
				strip.startTime = Time.time;
				strip.duration = hold;
				activeStrips[strip] = (strip.startTime, hold);
				StartCoroutine(HoldAndRelease(strip, hold));
			}

			yield return new WaitForSeconds(0.04f);
		}
	}

	private IEnumerator HoldAndRelease(QuadStrip s, float sec)
	{
		yield return new WaitForSeconds(sec);
		quadAllocator.ReleaseStrip(s);
		activeStrips.Remove(s);
	}

	private void OnGUI()
	{
		float now = Time.time;
		int y = pad;

		// TOP – Quad usage
		DrawQuadGrid(y);
		y += largeGridW + spacing;

		// MIDDLE – Index & Vertex side-by-side, centered below
		int totalSmallWidth = 2 * smallGridW + 30;
		int leftOffset = (largeGridW - totalSmallWidth) / 2 + pad;

		DrawAllocatorGrid(quadAllocator.IndexAllocator, y, leftOffset, "Index Blocks", new Color(1f, 0.6f, 0f, 1f));
		DrawAllocatorGrid(quadAllocator.VertexAllocator, y, leftOffset + smallGridW + 30, "Vertex Blocks", new Color(0.3f, 1f, 0.3f, 1f));

		GUI.color = Color.white;

		// RHS info
		int infoX = pad + largeGridW + 30;
		GUILayout.BeginArea(new Rect(infoX, pad, 360, 500));
		{
			GUILayout.Label("<b>QuadStrip Allocator Test</b>", Rich());
			GUILayout.Space(8);
			GUILayout.Label($"Active Strips: <color=yellow>{quadAllocator.ActiveStripCount}</color>");
			GUILayout.Label($"Index Blocks: <color=orange>{quadAllocator.IndexAllocator.AllocatedBlockCount}</color>/256");
			GUILayout.Label($"Vertex Blocks: <color=lime>{quadAllocator.VertexAllocator.AllocatedBlockCount}</color>/256");
			GUILayout.Space(8);
			GUILayout.Label("1 strip (1–6 quads) every 0.04s");
			GUILayout.Label("Lifetime: 0.5–3.0s (fade to black)");
		}
		GUILayout.EndArea();
	}

	private void DrawQuadGrid(int yStart)
	{
		for (int gy = 0; gy < 16; gy++)
			for (int gx = 0; gx < 16; gx++)
			{
				int id = gy * 16 + gx;
				Rect r = new Rect(pad + gx * largeCell, yStart + gy * largeCell, largeCell - 2, largeCell - 2);

				bool used = false;
				float maxI = 0f;
				foreach (var kv in activeStrips)
				{
					if (kv.Key.indexBlocks.Contains(id))
					{
						used = true;
						float t = Mathf.Clamp01((Time.time - kv.Value.start) / kv.Value.duration);
						maxI = Mathf.Max(maxI, 1f - t);
					}
				}
				GUI.color = used ? Color.Lerp(Color.black, Color.cyan, maxI) : Color.red;
				GUI.DrawTexture(r, Texture2D.whiteTexture);
			}
		GUI.color = Color.white;
		GUI.Label(new Rect(pad, yStart - 20, 300, 20), "<b>Quad Usage (per index block)</b>", Rich());
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