using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class TgaLoader
	{
		public static Texture2D LoadTGA(string filePath)
		{
			if (!File.Exists(filePath)) return null;
			return LoadTGA(File.ReadAllBytes(filePath));
		}

		public static Texture2D LoadTGA(byte[] tgaData)
		{
			if (tgaData == null || tgaData.Length < 18) return null;

			using (MemoryStream ms = new MemoryStream(tgaData))
			using (BinaryReader r = new BinaryReader(ms))
			{
				// Read TGA Header
				r.ReadByte();                       // ID Length
				r.ReadByte();                       // Color Map Type
				byte imageType = r.ReadByte();      // 2 = Uncompressed

				r.ReadUInt16(); r.ReadUInt16(); r.ReadByte(); // Skip color map
				r.ReadUInt16(); r.ReadUInt16();             // Origin X/Y

				ushort width = r.ReadUInt16();
				ushort height = r.ReadUInt16();
				byte bpp = r.ReadByte();
				byte descriptor = r.ReadByte();

				if (imageType != 2)
				{
					Debug.LogWarning($"Only uncompressed TGA (Type 2) supported. Got type {imageType}");
					return null;
				}

				// Note: We are using the logic that actually works for your files
				bool bottomUp = (descriptor & 0x20) == 0;
				Debug.Log($"[TGA] {width}x{height} | BPP={bpp} | BottomUp={bottomUp} | Descriptor=0x{descriptor:X2}");

				int bytesPerPixel = bpp / 8;
				Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
				Color32[] pixels = new Color32[width * height];

				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						byte b = r.ReadByte();
						byte g = r.ReadByte();
						byte rVal = r.ReadByte();
						byte a = (bytesPerPixel == 4) ? r.ReadByte() : (byte)255;

						// This logic works for your current TGA files
						int unityY = bottomUp ? y : (height - 1 - y);

						pixels[unityY * width + x] = new Color32(rVal, g, b, a);
					}
				}

				tex.SetPixels32(pixels);
				tex.Apply();
				tex.name = "LoadedTGA";

				return tex;
			}
		}
	}
}