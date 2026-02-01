using UnityEngine;

namespace MassiveHadronLtd
{
	public static class ColorPickerSquareUtility
	{
		public enum PickerStyle
		{
			SaturationValue_FixedHue,
			HueSaturation_FullValue,
		}

		public static Texture2D CreateColorPickerTexture(
			int size = 256,
			float alpha = 1f,
			PickerStyle style = PickerStyle.HueSaturation_FullValue,
			float fixedHue = 0f)
		{
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = "ColorPickerSquare"
			};

			Color[] pixels = new Color[size * size];
			float inv = 1f / (size - 1);

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float normX = x * inv;
					float normY = 1f - y * inv;

					Color col = (style == PickerStyle.HueSaturation_FullValue)
						? Color.HSVToRGB(normX, normY, 1f)
						: Color.HSVToRGB(fixedHue, normX, normY);

					col.a = alpha;
					pixels[y * size + x] = col;
				}
			}

			tex.SetPixels(pixels);
			tex.Apply(false);
			return tex;
		}

		public static Texture2D CreateValueSliderTexture(
			int width = 24,
			int height = 256,
			float hue = 0f,
			float saturation = 1f,
			float alpha = 1f)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = "ValueSlider"
			};

			Color[] pixels = new Color[width * height];
			float inv = 1f / (height - 1);

			for (int y = 0; y < height; y++)
			{
				float v = y * inv;  // bottom bright, top black (your new layout)

				Color col = Color.HSVToRGB(hue, saturation, v); // ← USE SATURATION
				col.a = alpha;

				for (int x = 0; x < width; x++)
					pixels[y * width + x] = col;
			}

			tex.SetPixels(pixels);
			tex.Apply(false);
			return tex;
		}


		public static Color GetColorAt(Texture2D texture, Vector2 normalizedUV)
		{
			if (texture == null) return Color.white;
			float x = Mathf.Clamp01(normalizedUV.x);
			float y = Mathf.Clamp01(normalizedUV.y);
			return texture.GetPixelBilinear(x, y);
		}

		public static Color GetColorFromLocalPoint(
			Texture2D texture,
			Vector2 localPointerPos,
			RectTransform rectTransform)
		{
			if (texture == null || rectTransform == null) return Color.white;

			Rect rect = rectTransform.rect;
			Vector2 uv = new Vector2(
				Mathf.InverseLerp(rect.xMin, rect.xMax, localPointerPos.x),
				Mathf.InverseLerp(rect.yMin, rect.yMax, localPointerPos.y)
			);

			return GetColorAt(texture, uv);
		}
	}
}
