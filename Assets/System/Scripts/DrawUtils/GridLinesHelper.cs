using UnityEngine;

namespace MassiveHadronLtd
{
	public static class GridLinesHelper
	{
		// Final colors
		private static readonly Color MainColor = new Color(0.3f, 0.3f, 0.9f, 0.5f);
		private static readonly Color ExtendedColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

		private static Material _lineMat;
		private static Material LineMat => _lineMat ??= MaterialUtils.CreateTransparentLineMaterial(Color.white);

		private static Material _mainMat;
		private static Material MainMat => _mainMat ??= MaterialUtils.CreateTransparentUnlitMaterial(MainColor);

		public static GameObject CreateGridLines(
			Transform parentTransform,
			int width,
			int height,
			int extension = 8,
			Color? extendedColor = null)
		{
			var existing = parentTransform.Find("GridLines");
			if (existing != null) Object.Destroy(existing.gameObject);

			var gridObj = new GameObject("GridLines");
			var t = gridObj.transform;
			t.SetParent(parentTransform, false);
			t.localPosition = Vector3.zero;

			var extColor = extendedColor ?? ExtendedColor;

			if (extension <= 0)
			{
				for (int x = 0; x <= width; x++) DrawMainLine(t, new(x, 0, 0), new(x, 0, height));
				for (int z = 0; z <= height; z++) DrawMainLine(t, new(0, 0, z), new(width, 0, z));
				return gridObj;
			}

			float x0 = -extension;
			float x1 = 0f;
			float x2 = width;
			float x3 = width + extension;

			float z0 = -extension;
			float z1 = 0f;
			float z2 = height;
			float z3 = height + extension;

			// Fade factor: 0 = fully visible (at main edge), 1 = fully transparent (at outer edge)
			float Fade(float coord, float inner, float outer)
			{
				if (coord >= inner && coord <= outer) return 0f;
				float dist = coord < inner ? inner - coord : coord - outer;
				return Mathf.Clamp01(dist / extension);
			}

			void DrawFadedLine(Vector3 a, Vector3 b, string name = "ExtLine")
			{
				float fadeA = 0f, fadeB = 0f;

				// X-axis fade
				fadeA = Mathf.Max(fadeA, Fade(a.x, 0f, width));
				fadeB = Mathf.Max(fadeB, Fade(b.x, 0f, width));

				// Z-axis fade
				fadeA = Mathf.Max(fadeA, Fade(a.z, 0f, height));
				fadeB = Mathf.Max(fadeB, Fade(b.z, 0f, height));

				// Corner bonus: multiplicative fade (feels more natural)
				if ((a.x < 0f || a.x > width) && (a.z < 0f || a.z > height))
					fadeA = 1f - (1f - fadeA) * (1f - Fade(a.z, 0f, height));
				if ((b.x < 0f || b.x > width) && (b.z < 0f || b.z > height))
					fadeB = 1f - (1f - fadeB) * (1f - Fade(b.z, 0f, height));

				var go = new GameObject(name);
				go.transform.SetParent(t, false);
				var lr = go.AddComponent<LineRenderer>();
				lr.material = LineMat;
				lr.startWidth = lr.endWidth = 0.025f;
				lr.positionCount = 2;
				lr.useWorldSpace = false;
				lr.SetPosition(0, a);
				lr.SetPosition(1, b);

				Color cA = extColor; cA.a *= (1f - fadeA);
				Color cB = extColor; cB.a *= (1f - fadeB);

				lr.startColor = cA;
				lr.endColor = cB;
			}

			// === CORNER ZONES (own outer border) ===
			for (int i = 0; i <= extension; i++)
			{
				float px, pz;

				// Top-Left
				px = x0 + i; pz = z0 + i;
				DrawFadedLine(new(px, 0, z0), new(px, 0, z1), $"TL_V_{i}");
				DrawFadedLine(new(x0, 0, pz), new(x1, 0, pz), $"TL_H_{i}");

				// Top-Right
				px = x2 + i;
				DrawFadedLine(new(px, 0, z0), new(px, 0, z1), $"TR_V_{i}");
				DrawFadedLine(new(x2, 0, pz), new(x3, 0, pz), $"TR_H_{i}");

				// Bottom-Left
				px = x0 + i; pz = z2 + i;
				DrawFadedLine(new(px, 0, z2), new(px, 0, z3), $"BL_V_{i}");
				DrawFadedLine(new(x0, 0, pz), new(x1, 0, pz), $"BL_H_{i}");

				// Bottom-Right
				px = x2 + i;
				DrawFadedLine(new(px, 0, z2), new(px, 0, z3), $"BR_V_{i}");
				DrawFadedLine(new(x2, 0, pz), new(x3, 0, pz), $"BR_H_{i}");
			}

			// === EDGE ZONES (inner sides only) ===
			for (int x = 1; x < width; x++)
			{
				DrawFadedLine(new(x, 0, z2), new(x, 0, z3), $"Top_V_{x}");
				DrawFadedLine(new(x, 0, z0), new(x, 0, z1), $"Bot_V_{x}");
			}
			for (int z = 1; z < height; z++)
			{
				DrawFadedLine(new(x0, 0, z), new(x1, 0, z), $"Left_H_{z}");
				DrawFadedLine(new(x2, 0, z), new(x3, 0, z), $"Right_H_{z}");
			}
			for (int i = 1; i < extension; i++)
			{
				DrawFadedLine(new(x1, 0, z2 + i), new(x2, 0, z2 + i), $"Top_H_{i}");
				DrawFadedLine(new(x1, 0, z0 + i), new(x2, 0, z0 + i), $"Bot_H_{i}");
				DrawFadedLine(new(x0 + i, 0, z1), new(x0 + i, 0, z2), $"Left_V_{i}");
				DrawFadedLine(new(x2 + i, 0, z1), new(x2 + i, 0, z2), $"Right_V_{i}");
			}

			// === MAIN GRID (solid) ===
			for (int x = 0; x <= width; x++)
				DrawMainLine(t, new(x, 0, 0), new(x, 0, height));
			for (int z = 0; z <= height; z++)
				DrawMainLine(t, new(0, 0, z), new(width, 0, z));

			return gridObj;
		}

		private static void DrawMainLine(Transform parent, Vector3 a, Vector3 b)
		{
			var go = new GameObject("MainLine");
			go.transform.SetParent(parent, false);
			var lr = go.AddComponent<LineRenderer>();
			lr.material = MainMat;
			lr.startWidth = lr.endWidth = 0.02f;
			lr.positionCount = 2;
			lr.useWorldSpace = false;
			lr.SetPosition(0, a);
			lr.SetPosition(1, b);
		}
	}
}