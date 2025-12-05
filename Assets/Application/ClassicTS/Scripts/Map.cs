using System;
using UnityEngine;
using Newtonsoft.Json;
using MassiveHadronLtd;
using System.Linq;

namespace ClassicTilestorm
{
	[Serializable]
	public class Map
	{
		public string name;
		public string character;
		public string music;
		public string button;
		public int width;
		public int height;

		public int[] waypoints;
		public string[] table;
		public int[] tiles;
		public int[] solve;

		public MapAttachment[] attachments = Array.Empty<MapAttachment>();

		// Atomic fields - ignored during normal serialization
		[JsonIgnore] public Definition[] definitions;
		[JsonIgnore] public TextureSequence[] textures;
		[JsonIgnore] public string version = "1.0";
		[JsonIgnore] public string author = "Player";
		[JsonIgnore] public string exportedFrom = "ClassicTilestorm";

		public bool ShouldSerializesolve() => solve != null && solve.Length > 0;
		public bool ShouldSerializewaypoints() => waypoints != null && waypoints.Length > 0;
		public bool ShouldSerializeattachments() => attachments != null && attachments.Length > 0;

		[JsonIgnore] public bool IsAtomic => definitions?.Length > 0 || textures?.Length > 0;

		/// <summary>
		/// Rebuilds the optimal frequency-sorted table and remaps tiles.
		/// Returns true if any changes were made (table or tiles changed).
		/// </summary>
		public bool Consolidate()
		{
			if (tiles == null || tiles.Length == 0)
				return false;

			var map_definitions = new string[tiles.Length];
			for (int i = 0; i < tiles.Length; i++)
			{
				int idx = tiles[i];
				if (idx >= 0 && table != null && idx < table.Length)
					map_definitions[i] = table[idx];
				else
					map_definitions[i] = "tile_empty";
			}

			bool changed = false;

			var newFrequencyTable = map_definitions.ToFrequencySortedTable();

			if (table == null || !table.SequenceEqual(newFrequencyTable))
			{
				table = newFrequencyTable;
				changed = true;
			}

			if (changed || tiles == null || tiles.Length != map_definitions.Length)
			{
				tiles = new int[map_definitions.Length];

				for (int i = 0; i < map_definitions.Length; i++)
				{
					string id = map_definitions[i];
					if (string.IsNullOrEmpty(id))
					{
						Debug.LogError($"Invalid ID at index {i}");
						tiles[i] = -1;
						continue;
					}

					int newIndex = Array.IndexOf(table, id);
					tiles[i] = newIndex != -1 ? newIndex : -1;
				}
			}

			if (changed) Debug.Log($"{name} consolidated");
			return changed;
		}

		public enum Anchor
		{
			TopLeft, TopCenter, TopRight,
			MiddleLeft, Center, MiddleRight,
			BottomLeft, BottomCenter, BottomRight
		}

		// KEEP YOUR ORIGINAL PUBLIC Resize() — IT IS PERFECT FOR EDITOR USE
		public bool Resize(int newWidth, int newHeight, Anchor anchor = Anchor.Center)
		{
			if (newWidth <= 0 || newHeight <= 0) return false;
			if (width == newWidth && height == newHeight) return true;

			int oldWidth = width;
			int oldHeight = height;

			// Calculate proper offset from anchor
			int offsetX = anchor switch
			{
				Anchor.TopLeft or Anchor.MiddleLeft or Anchor.BottomLeft => 0,
				Anchor.TopCenter or Anchor.Center or Anchor.BottomCenter => (newWidth - oldWidth) / 2,
				Anchor.TopRight or Anchor.MiddleRight or Anchor.BottomRight => newWidth - oldWidth,
				_ => (newWidth - oldWidth) / 2
			};

			int offsetZ = anchor switch
			{
				Anchor.TopLeft or Anchor.TopCenter or Anchor.TopRight => 0,
				Anchor.MiddleLeft or Anchor.Center or Anchor.MiddleRight => (newHeight - oldHeight) / 2,
				Anchor.BottomLeft or Anchor.BottomCenter or Anchor.BottomRight => newHeight - oldHeight,
				_ => (newHeight - oldHeight) / 2
			};

			// Use the real engine
			bool success = RepositionAndResize(newWidth, newHeight, offsetX, offsetZ);

			if (success)
				Consolidate();

			if (success)
				Debug.Log($"Map '{name}' resized to {newWidth}x{newHeight} (anchor: {anchor}).");

			return success;
		}

		// THE REAL, BULLETPROOF ENGINE — does everything correctly
		public bool RepositionAndResize(int newWidth, int newHeight, int offsetX, int offsetZ)
		{
			if (newWidth <= 0 || newHeight <= 0) return false;

			int oldWidth = width;
			int oldHeight = height;
			int newSize = newWidth * newHeight;

			// Ensure tile_empty
			int emptyIndex = table != null ? Array.IndexOf(table, "tile_empty") : -1;
			if (emptyIndex == -1 && table != null)
			{
				var list = table.ToList();
				list.Add("tile_empty");
				table = list.ToArray();
				emptyIndex = table.Length - 1;
			}

			var newTiles = new int[newSize];
			Array.Fill(newTiles, emptyIndex);

			// Copy tiles with arbitrary offset (positive or negative!)
			for (int z = 0; z < oldHeight; z++)
				for (int x = 0; x < oldWidth; x++)
				{
					int oldIdx = z * oldWidth + x;
					if (oldIdx >= tiles.Length) continue;

					int nx = x + offsetX;
					int nz = z + offsetZ;

					if (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight)
					{
						newTiles[nz * newWidth + nx] = tiles[oldIdx];
					}
				}

			// solve[] — fully preserved
			var newSolve = new int[newSize];
			if (solve != null && solve.Length == oldWidth * oldHeight)
			{
				for (int z = 0; z < oldHeight; z++)
					for (int x = 0; x < oldWidth; x++)
					{
						int oldIdx = z * oldWidth + x;
						int delta = solve[oldIdx];
						if (delta == 0) continue;

						int srcIdx = oldIdx + delta;
						if (srcIdx < 0 || srcIdx >= solve.Length) continue;

						int srcX = srcIdx % oldWidth;
						int srcZ = srcIdx / oldWidth;

						int nx = x + offsetX;
						int nz = z + offsetZ;
						int nsx = srcX + offsetX;
						int nsz = srcZ + offsetZ;

						if (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight &&
							nsx >= 0 && nsx < newWidth && nsz >= 0 && nsz < newHeight)
						{
							int newPos = nz * newWidth + nx;
							int newSrc = nsz * newWidth + nsx;
							newSolve[newPos] = newSrc - newPos;
						}
					}
			}

			// Waypoints & attachments
			int Remap(int idx)
			{
				if (idx < 0) return idx;
				int x = idx % oldWidth;
				int z = idx / oldWidth;
				int nx = x + offsetX;
				int nz = z + offsetZ;
				return (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight) ? nz * newWidth + nx : -1;
			}

			if (waypoints != null) for (var n = 0; n < waypoints.Length; ++n) waypoints[n] = Remap(waypoints[n]);
			if (attachments != null) foreach (var a in attachments) a.tile = Remap(a.tile);

			// Apply
			width = newWidth;
			height = newHeight;
			tiles = newTiles;
			solve = newSolve;

			return true;
		}

		public bool CropToContent()
		{
			var (minX, minZ, maxX, maxZ) = GetContentBounds();
			if (maxX < 0) return false;

			int w = maxX - minX + 1;
			int h = maxZ - minZ + 1;

			bool success = RepositionAndResize(w, h, -minX, -minZ);

			if (success)
				Consolidate();

			return success;
		}

		/// <summary>
		/// Returns the actual used bounds of the map (non-empty tiles only).
		/// </summary>
		public (int minX, int minZ, int maxX, int maxZ) GetContentBounds()
		{
			if (tiles == null || tiles.Length == 0 || width <= 0 || height <= 0)
				return (0, 0, -1, -1);

			int minX = width;
			int minZ = height;
			int maxX = -1;
			int maxZ = -1;

			int emptyIdx = table != null ? Array.IndexOf(table, "tile_empty") : -1;

			for (int i = 0; i < tiles.Length; i++)
			{
				int t = tiles[i];
				if (t < 0 || t == emptyIdx || (table != null && t < table.Length && table[t] == "tile_empty"))
					continue;

				int x = i % width;
				int z = i / width;

				if (x < minX) minX = x;
				if (x > maxX) maxX = x;
				if (z < minZ) minZ = z;
				if (z > maxZ) maxZ = z;
			}

			return maxX >= 0 ? (minX, minZ, maxX, maxZ) : (0, 0, -1, -1);
		}

		/// <summary>
		/// Returns a cropped copy of this map for serialization/export only.
		/// Original map is untouched. Used automatically during export.
		/// </summary>
		public Map CreateCroppedCopy()
		{
			var copy = new Map
			{
				name = name,
				character = character,
				music = music,
				button = button,
				width = width,
				height = height,

				// These are the ONLY fields CropToContent() and RepositionAndResize() mutate:
				waypoints = waypoints != null ? (int[])waypoints.Clone() : null,
				tiles = tiles != null ? (int[])tiles.Clone() : null,
				solve = solve != null ? (int[])solve.Clone() : null,
				table = table != null ? (string[])table.Clone() : null,

				// attachments: we need real copies because Remap(ref a.tile) modifies them
				attachments = null != attachments ? attachments.Select(a => a.ShallowClone()).ToArray() : Array.Empty<MapAttachment>()
			};

			bool cropped = copy.CropToContent();

			if (cropped)
				Debug.Log($"[Export] Map '{copy.name}' auto-cropped to {copy.width}x{copy.height}");

			return copy;
		}
	}
}