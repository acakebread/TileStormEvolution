using UnityEngine;

namespace MassiveHadronLtd
{
	public static class CubemapUtility
	{
		public static Color ComputeBrightColor(Cubemap cubemap, float threshold = 0.85f)
		{
			if (cubemap == null)
				return Color.white;

			if (!cubemap.isReadable)
			{
				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
				return Color.white;
			}

			// === Build one large array containing ALL pixels from all 6 faces ===
			var faceSize = cubemap.width;
			var pixels = new Color[faceSize * faceSize * 6];//totalPixels

			for (var i = 0; i < 6; i++)
			{
				var face = cubemap.GetPixels((CubemapFace)i);
				System.Array.Copy(face, 0, pixels, faceSize * faceSize * i, face.Length);
			}

			return ColourUtils.ThresholdColour(pixels, threshold);
		}
	}
}

