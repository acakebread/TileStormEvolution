using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(VertexAllocator))]
public class VertexAllocatorTest : MonoBehaviour
{
	private VertexAllocator allocator;
	private readonly System.Random rnd = new System.Random();

	// Track lifetime for color fade
	private readonly Dictionary<int, (float startTime, float duration)> activeAllocations = new();

	private void Awake() => allocator = GetComponent<VertexAllocator>();
	private void Start() => StartCoroutine(TestRoutine());

	private IEnumerator TestRoutine()
	{
		while (true)
		{
			for (int n = 0; n < 4; ++n)
			{
				if (allocator.FreeBlockCount > 0)
				{
					int blockId = allocator.Allocate();
					if (blockId != -1)
					{
						float holdTime = 1.0f + (float)rnd.NextDouble() * 3.0f; // 1.0–4.0 s
						float startTime = Time.time;
						activeAllocations[blockId] = (startTime, holdTime);
						StartCoroutine(HoldAndRelease(blockId, holdTime));
					}
				}
			}

			yield return new WaitForSeconds(0.04f);
		}
	}

	private IEnumerator HoldAndRelease(int blockId, float seconds)
	{
		yield return new WaitForSeconds(seconds);
		allocator.Release(blockId);
		activeAllocations.Remove(blockId);
	}

	private void OnGUI()
	{
		const int cell = 22;
		const int pad = 60;
		float now = Time.time;

		// Draw 16×16 grid
		for (int y = 0; y < 16; y++)
			for (int x = 0; x < 16; x++)
			{
				int blockId = y * 16 + x;
				Rect r = new Rect(pad + x * cell, pad + y * cell, cell - 2, cell - 2);

				if (activeAllocations.TryGetValue(blockId, out var data))
				{
					float elapsed = now - data.startTime;
					float t = Mathf.Clamp01(elapsed / data.duration);
					float intensity = 1.0f - t;
					GUI.color = new Color(0, intensity, intensity, 1); // Bright cyan to black
				}
				else
				{
					GUI.color = Color.red;
				}

				GUI.DrawTexture(r, Texture2D.whiteTexture);
			}

		GUI.color = Color.white;

		// Info panel on the RIGHT-HAND SIDE
		int infoX = pad + 16 * cell + 20;
		int infoWidth = 300;
		GUILayout.BeginArea(new Rect(infoX, pad, infoWidth, 120));
		{
			GUILayout.Label($"<b>Vertex Allocator Test</b>", new GUIStyle(GUI.skin.label) { richText = true });
			GUILayout.Space(4);
			GUILayout.Label($"• Allocated: <color=yellow>{allocator.AllocatedBlockCount}</color>/256 blocks");
			GUILayout.Label($"• Free: <color=cyan>{allocator.FreeBlockCount}</color>");
			GUILayout.Space(4);
			GUILayout.Label("<color=#00FFFF>Bright cyan</color> to <color=#000033>black</color> = lifetime fade (1–4s)");
			GUILayout.Label("4 blocks (2 vertices each) every 0.04s");
		}
		GUILayout.EndArea();
	}
}