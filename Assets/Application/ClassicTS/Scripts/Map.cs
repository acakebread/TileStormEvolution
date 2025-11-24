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

		public Waypoint[] waypoints;
		public string[] table;
		public int[] tiles;
		public int[] mixed;
		public Pickups Pickups;

		// Atomic fields - ignored during normal serialization
		[JsonIgnore] public Definition[] definitions;
		[JsonIgnore] public TextureSequence[] textures;
		[JsonIgnore] public string version = "1.0";
		[JsonIgnore] public string author = "Player";
		[JsonIgnore] public string exportedFrom = "ClassicTilestorm";

		public bool ShouldSerializePickups() => Pickups != null && Pickups.nPickupCount > 0;

		[JsonIgnore] public bool IsAtomic => definitions?.Length > 0 || textures?.Length > 0;

		/// <summary>
		/// Rebuilds the optimal frequency-sorted table and remaps tiles.
		/// Returns true if any changes were made (table or tiles changed).
		/// </summary>
		public bool Consolidate(string[] definitions)
		{
			if (definitions == null) throw new ArgumentNullException(nameof(definitions));

			bool changed = false;

			// Step 1: Build the new optimal frequency-sorted table
			var newFrequencyTable = definitions.ToFrequencySortedTable();

			// Fast comparison: first check reference equality (common case when nothing changed)
			// Then structural equality using SequenceEqual (very fast for arrays)
			if (table == null || !table.SequenceEqual(newFrequencyTable))
			{
				table = newFrequencyTable;
				changed = true;
			}

			// Step 2: Only remap tiles if the table actually changed
			// (This avoids unnecessary work and prevents false-positive tile changes)
			if (changed || tiles == null || tiles.Length != definitions.Length)
			{
				var oldTiles = tiles; // Keep for potential future diff logging if needed
				tiles = new int[definitions.Length];

				for (int i = 0; i < definitions.Length; i++)
				{
					string id = definitions[i];
					if (string.IsNullOrEmpty(id))
					{
						Debug.LogError($"Invalid ID at index {i}");
						tiles[i] = -1;
						continue;
					}

					int newIndex = Array.IndexOf(table, id);
					if (newIndex == -1)
					{
						Debug.LogError($"Definition '{id}' not found in frequency table! This should not happen.");
						tiles[i] = -1;
					}
					else
					{
						tiles[i] = newIndex;
					}
				}

				// If you want to detect if tiles actually changed (beyond table change), uncomment:
				// if (oldTiles != null && !oldTiles.SequenceEqual(tiles)) changed = true;
			}
			if (changed) Debug.Log($"{name} consolidated");
			return changed;
		}



		// Add this inside the Map class (at the bottom is fine)
		public enum Anchor
		{
			TopLeft, TopCenter, TopRight,
			MiddleLeft, Center, MiddleRight,
			BottomLeft, BottomCenter, BottomRight
		}

		public bool Resize(int newWidth, int newHeight, Anchor anchor = Anchor.Center)
		{
			if (newWidth <= 0 || newHeight <= 0) return false;
			if (width == newWidth && height == newHeight) return true;

			int oldWidth = width;
			int oldHeight = height;

			// Compute anchor offset
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

			// === 1. Safety: reject if non-empty tile would be lost ===
			if (tiles != null)
			{
				for (int z = 0; z < oldHeight; z++)
					for (int x = 0; x < oldWidth; x++)
					{
						int idx = z * oldWidth + x;
						if (idx >= tiles.Length) continue;
						if (tiles[idx] < 0) continue;
						if (table == null || tiles[idx] >= table.Length || table[tiles[idx]] == "tile_empty") continue;

						int nx = x + offsetX;
						int nz = z + offsetZ;
						if (nx < 0 || nx >= newWidth || nz < 0 || nz >= newHeight)
						{
							Debug.LogWarning($"Resize rejected: non-empty tile at ({x},{z}) would be lost.");
							return false;
						}
					}
			}

			int newSize = newWidth * newHeight;

			// Pre-cache empty index once
			int emptyIndex = table != null ? Array.IndexOf(table, "tile_empty") : -1;
			if (emptyIndex == -1 && table != null)
			{
				var list = table.ToList();
				list.Add("tile_empty");
				table = list.ToArray();
				emptyIndex = table.Length - 1;
			}

			// === 2. Build new tiles ===
			var newTiles = new int[newSize];
			for (int i = 0; i < newSize; i++) newTiles[i] = emptyIndex;

			if (tiles != null)
			{
				for (int z = 0; z < oldHeight; z++)
					for (int x = 0; x < oldWidth; x++)
					{
						int oldIdx = z * oldWidth + x;
						if (oldIdx >= tiles.Length) continue;

						int nx = x + offsetX;
						int nz = z + offsetZ;
						if (nx < 0 || nx >= newWidth || nz < 0 || nz >= newHeight) continue;

						newTiles[nz * newWidth + nx] = tiles[oldIdx];
					}
			}

			// === 3. Remap waypoints ===
			if (waypoints != null)
			{
				for (int i = 0; i < waypoints.Length; i++)
				{
					int idx = waypoints[i].nTile;
					if (idx < 0) continue;
					int x = idx % oldWidth;
					int z = idx / oldWidth;
					int nx = x + offsetX;
					int nz = z + offsetZ;
					waypoints[i].nTile = (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight)
						? nz * newWidth + nx
						: -1;
				}
			}

			// === 4. Remap mixed[] — YOUR GENIUS, OPTIMIZED ===
			var newMixed = new int[newSize]; // defaults to 0

			if (mixed != null && mixed.Length == oldWidth * oldHeight)
			{
				for (int z = 0; z < oldHeight; z++)
					for (int x = 0; x < oldWidth; x++)
					{
						int oldIdx = z * oldWidth + x;
						int oldDelta = mixed[oldIdx];
						if (oldDelta == 0) continue;

						int oldSourceIdx = oldIdx + oldDelta;
						if (oldSourceIdx < 0 || oldSourceIdx >= mixed.Length) continue;

						int srcX = oldSourceIdx % oldWidth;
						int srcZ = oldSourceIdx / oldWidth;

						int newX = x + offsetX;
						int newZ = z + offsetZ;
						int newSrcX = srcX + offsetX;
						int newSrcZ = srcZ + offsetZ;

						if (newX < 0 || newX >= newWidth || newZ < 0 || newZ >= newHeight) continue;
						if (newSrcX < 0 || newSrcX >= newWidth || newSrcZ < 0 || newSrcZ >= newHeight) continue;

						int newPos = newZ * newWidth + newX;
						int newSourcePos = newSrcZ * newWidth + newSrcX;
						newMixed[newPos] = newSourcePos - newPos;
					}
			}

			// === 5. Apply ===
			width = newWidth;
			height = newHeight;
			tiles = newTiles;
			mixed = newMixed;

			Debug.Log($"Map '{name}' resized to {newWidth}x{newHeight} (anchor: {anchor}). mixed[] fully preserved.");
			return true;
		}
	}

	[Serializable]
	public class Pickups { public int nPickupCount; }
}