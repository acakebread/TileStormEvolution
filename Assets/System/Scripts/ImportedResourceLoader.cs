using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using ClassicTilestorm.Assets;

namespace MassiveHadronLtd
{
	public static class ImportedResourceLoader
	{
		private static readonly Dictionary<string, Texture> TextureCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, Material> MaterialCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, AudioClip> AudioCache = new(StringComparer.OrdinalIgnoreCase);

		public static void ClearCache()
		{
			TextureCache.Clear();
			MaterialCache.Clear();
			AudioCache.Clear();
		}

		public static bool TryLoadTexture(string path, out Texture texture)
		{
			texture = null;
			if (string.IsNullOrWhiteSpace(path))
				return false;

			var normalized = NormalizePath(path);
			if (!File.Exists(normalized))
				return false;

			if (TextureCache.TryGetValue(normalized, out var cached) && cached != null)
			{
				texture = cached;
				return true;
			}

			texture = LoadTextureFromFile(normalized);
			if (texture != null)
			{
				TextureCache[normalized] = texture;
				return true;
			}

			return false;
		}

		public static bool TryLoadPortableMaterial(string path, out Material material)
		{
			material = null;
			if (string.IsNullOrWhiteSpace(path))
				return false;

			var normalized = NormalizePath(path);
			if (!File.Exists(normalized))
				return false;

			if (MaterialCache.TryGetValue(normalized, out var cached) && cached != null)
			{
				material = cached;
				return true;
			}

			try
			{
				var json = File.ReadAllText(normalized);
				var portable = JsonConvert.DeserializeObject<PortableMaterial>(json);
				material = portable?.ToUnityMaterial();
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ImportedResourceLoader: failed to load portable material '{normalized}': {ex.Message}");
				material = null;
			}

			if (material != null)
			{
				MaterialCache[normalized] = material;
				return true;
			}

			return false;
		}

		public static bool TryLoadAudioClip(string path, out AudioClip clip)
		{
			clip = null;
			if (string.IsNullOrWhiteSpace(path))
				return false;

			var normalized = NormalizePath(path);
			if (!File.Exists(normalized))
				return false;

			if (AudioCache.TryGetValue(normalized, out var cached) && cached != null)
			{
				clip = cached;
				return true;
			}

			try
			{
				var ext = Path.GetExtension(normalized).ToLowerInvariant();
				if (ext == ".wav")
				{
					clip = WavAudioUtility.LoadFromFile(normalized);
				}
				else
				{
					Debug.LogWarning($"ImportedResourceLoader: unsupported audio format '{ext}' for '{normalized}'. WAV is currently supported for imported audio.");
					clip = null;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ImportedResourceLoader: failed to load audio clip '{normalized}': {ex.Message}");
				clip = null;
			}

			if (clip != null)
			{
				AudioCache[normalized] = clip;
				return true;
			}

			return false;
		}

		private static Texture LoadTextureFromFile(string path)
		{
			try
			{
				var ext = Path.GetExtension(path).ToLowerInvariant();
				switch (ext)
				{
					case ".tga":
						return TgaLoader.LoadTGA(File.ReadAllBytes(path));

					case ".png":
					case ".jpg":
					case ".jpeg":
						{
							var bytes = File.ReadAllBytes(path);
							var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
							if (tex.LoadImage(bytes))
							{
								tex.name = Path.GetFileNameWithoutExtension(path);
								return tex;
							}
							UnityEngine.Object.DestroyImmediate(tex);
							return null;
						}

					default:
						Debug.LogWarning($"ImportedResourceLoader: unsupported texture format '{ext}' for '{path}'.");
						return null;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ImportedResourceLoader: failed to load texture '{path}': {ex.Message}");
				return null;
			}
		}

		private static string NormalizePath(string value)
			=> string.IsNullOrWhiteSpace(value) ? null : value.Replace('\\', '/').Trim();
	}

	public static class WavAudioUtility
	{
		public static AudioClip LoadFromFile(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
				return null;

			using var stream = File.OpenRead(path);
			using var reader = new BinaryReader(stream);

			if (ReadFourCC(reader) != "RIFF")
				return null;

			reader.ReadInt32();
			if (ReadFourCC(reader) != "WAVE")
				return null;

			short audioFormat = 0;
			short channels = 0;
			int sampleRate = 0;
			short bitsPerSample = 0;
			byte[] data = null;

			while (stream.Position < stream.Length)
			{
				var chunkId = ReadFourCC(reader);
				if (string.IsNullOrEmpty(chunkId))
					break;

				int chunkSize = reader.ReadInt32();
				long chunkEnd = stream.Position + chunkSize;

				if (chunkId == "fmt ")
				{
					audioFormat = reader.ReadInt16();
					channels = reader.ReadInt16();
					sampleRate = reader.ReadInt32();
					reader.ReadInt32(); // byte rate
					reader.ReadInt16(); // block align
					bitsPerSample = reader.ReadInt16();

					if (chunkSize > 16)
						stream.Position = chunkEnd;
				}
				else if (chunkId == "data")
				{
					data = reader.ReadBytes(chunkSize);
				}
				else
				{
					stream.Position = chunkEnd;
				}

				if ((chunkSize & 1) == 1)
					stream.Position++;
			}

			if (data == null || channels <= 0 || sampleRate <= 0)
				return null;

			float[] samples;
			if (audioFormat == 1)
			{
				samples = DecodePcm(data, bitsPerSample);
			}
			else if (audioFormat == 3)
			{
				samples = DecodeFloat32(data);
			}
			else
			{
				Debug.LogWarning($"WavAudioUtility: unsupported WAV format {audioFormat} in '{path}'.");
				return null;
			}

			if (samples == null || samples.Length == 0)
				return null;

			int sampleCount = samples.Length / channels;
			var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), sampleCount, channels, sampleRate, false);
			clip.SetData(samples, 0);
			return clip;
		}

		private static float[] DecodePcm(byte[] data, short bitsPerSample)
		{
			if (bitsPerSample == 16)
			{
				int sampleCount = data.Length / 2;
				var samples = new float[sampleCount];
				for (int i = 0; i < sampleCount; i++)
				{
					short sample = BitConverter.ToInt16(data, i * 2);
					samples[i] = sample / 32768f;
				}
				return samples;
			}

			if (bitsPerSample == 8)
			{
				int sampleCount = data.Length;
				var samples = new float[sampleCount];
				for (int i = 0; i < sampleCount; i++)
					samples[i] = (data[i] - 128) / 128f;
				return samples;
			}

			if (bitsPerSample == 24)
			{
				int sampleCount = data.Length / 3;
				var samples = new float[sampleCount];
				for (int i = 0; i < sampleCount; i++)
				{
					int offset = i * 3;
					int sample = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
					if ((sample & 0x800000) != 0)
						sample |= unchecked((int)0xFF000000);
					samples[i] = sample / 8388608f;
				}
				return samples;
			}

			Debug.LogWarning($"WavAudioUtility: unsupported PCM bit depth {bitsPerSample}.");
			return null;
		}

		private static float[] DecodeFloat32(byte[] data)
		{
			int sampleCount = data.Length / 4;
			var samples = new float[sampleCount];
			for (int i = 0; i < sampleCount; i++)
				samples[i] = BitConverter.ToSingle(data, i * 4);
			return samples;
		}

		private static string ReadFourCC(BinaryReader reader)
		{
			var bytes = reader.ReadBytes(4);
			return bytes.Length == 4 ? System.Text.Encoding.ASCII.GetString(bytes) : null;
		}
	}
}
