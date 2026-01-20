using System;
using UnityEngine;
using Newtonsoft.Json;
using MassiveHadronLtd;
using System.Linq;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	[Serializable]
	public class Map
	{
		//private struct TileEntry
		//{
		//	public string StableId;     // hashid or null
		//	public string DisplayName;  // current id / future name

		//	public TileEntry(string displayName, string stableId = null)
		//	{
		//		DisplayName = displayName ?? "tile_empty";
		//		StableId = stableId;
		//	}
		//}

		public string name;
		public string character;
		public string music;
		public string skybox;
		public string button;
		public int width;
		public int height;

		public int[] waypoints;
		public string[] table;
		public int[] tiles;
		public int[] solve;

		[JsonIgnore]
		private List<string> _stableIds = new List<string>();  // index-aligned with public table

		// Helper to keep them in sync (call this instead of modifying table directly)
		public void SetTileTypeAtIndex(int index, string displayName, string stableId = null)
		{
			// Extend lists if needed
			while (_stableIds.Count <= index)
				_stableIds.Add(null);

			while (table == null || table.Length <= index)
			{
				var newTable = table != null ? table.ToList() : new List<string>();
				newTable.Add("tile_empty");
				table = newTable.ToArray();
			}

			_stableIds[index] = stableId;
			table[index] = displayName ?? "tile_empty";
		}

		// ── For atomic export only (called from ExportAtomicMap) ──────────────────
		public IEnumerable<string> GetEnrichedTableForExport()
		{
			if (table == null) yield break;

			for (int i = 0; i < table.Length; i++)
			{
				string name = table[i] ?? "tile_empty";
				string hash = (i < _stableIds.Count) ? _stableIds[i] : null;

				yield return string.IsNullOrEmpty(hash) ? name : $"[{hash}]{name}";
			}
		}


		public MapAttachment[] attachments = Array.Empty<MapAttachment>();

		public bool ShouldSerializeskybox() => !string.IsNullOrEmpty(skybox);
		public bool ShouldSerializesolve() => solve != null && solve.Length > 0;
		public bool ShouldSerializewaypoints() => waypoints != null && waypoints.Length > 0;
		public bool ShouldSerializeattachments() => attachments != null && attachments.Length > 0;

		public bool IsValidTile(int index) => index >= 0 && index < width * height;

		/// <summary>
		/// Rebuilds the optimal frequency-sorted table and remaps tiles.
		/// Returns true if any changes were made (table or tiles changed).
		/// </summary>
		public bool Consolidate()
		{
			if (tiles == null || tiles.Length == 0)
				return false;

			// 1. Build name → stableId lookup (from current state)
			var nameToStable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < table.Length && i < _stableIds.Count; i++)
			{
				string name = table[i];
				string stable = _stableIds[i];

				if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(stable))
				{
					// If collision (same name, different stable) → keep first seen
					if (!nameToStable.ContainsKey(name))
						nameToStable[name] = stable;
				}
			}

			// 2. Build current definitions for frequency sort
			var mapDefinitions = new string[tiles.Length];
			for (int i = 0; i < tiles.Length; i++)
			{
				int idx = tiles[i];
				mapDefinitions[i] = (idx >= 0 && idx < table.Length) ? table[idx] ?? "tile_empty" : "tile_empty";
			}

			var newFrequencyTable = mapDefinitions.ToFrequencySortedTable();

			bool changed = table == null || !table.SequenceEqual(newFrequencyTable);

			if (changed)
			{
				// 3. Rebuild _stableIds in new order
				var newStableIds = new List<string>(newFrequencyTable.Length);

				foreach (string name in newFrequencyTable)
				{
					nameToStable.TryGetValue(name, out string carriedStable);
					newStableIds.Add(carriedStable);  // null if no previous stable ID
				}

				_stableIds = newStableIds;
				table = newFrequencyTable;
			}

			// 4. Rebuild tiles[] indices
			if (changed || tiles.Length != mapDefinitions.Length)
			{
				tiles = new int[mapDefinitions.Length];
				for (int i = 0; i < mapDefinitions.Length; i++)
				{
					string name = mapDefinitions[i];
					int newIndex = Array.IndexOf(table, name);
					tiles[i] = newIndex != -1 ? newIndex : -1;
				}
			}

			if (changed) Debug.Log($"{name} consolidated (stable IDs preserved)");

			return changed;
		}
		//public bool Consolidate()
		//{
		//	if (tiles == null || tiles.Length == 0)
		//		return false;

		//	var map_definitions = new string[tiles.Length];
		//	for (int i = 0; i < tiles.Length; i++)
		//	{
		//		int idx = tiles[i];
		//		if (idx >= 0 && table != null && idx < table.Length)
		//			map_definitions[i] = table[idx];
		//		else
		//			map_definitions[i] = "tile_empty";
		//	}

		//	bool changed = false;

		//	var newFrequencyTable = map_definitions.ToFrequencySortedTable();

		//	if (table == null || !table.SequenceEqual(newFrequencyTable))
		//	{
		//		table = newFrequencyTable;
		//		changed = true;
		//	}

		//	if (changed || tiles == null || tiles.Length != map_definitions.Length)
		//	{
		//		tiles = new int[map_definitions.Length];

		//		for (int i = 0; i < map_definitions.Length; i++)
		//		{
		//			string id = map_definitions[i];
		//			if (string.IsNullOrEmpty(id))
		//			{
		//				Debug.LogError($"Invalid ID at index {i}");
		//				tiles[i] = -1;
		//				continue;
		//			}

		//			int newIndex = Array.IndexOf(table, id);
		//			tiles[i] = newIndex != -1 ? newIndex : -1;
		//		}
		//	}

		//	if (changed) Debug.Log($"{name} consolidated");
		//	return changed;
		//}

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

			// Copy the private lists (they are List<T>, so we can clone contents)
			copy._stableIds = _stableIds != null ? new List<string>(_stableIds) : new List<string>();
			//copy._displayNames = _displayNames != null ? new List<string>(_displayNames) : new List<string>();

			bool cropped = copy.CropToContent();

			if (cropped)
				Debug.Log($"[Export] Map '{copy.name}' auto-cropped to {copy.width}x{copy.height}");

			return copy;
		}

		/// <summary>
		/// After deserialization, parse any enriched "[hash]name" entries in table.
		/// Splits them → populates internal _stableIds, strips prefix from public table.
		/// Call this immediately after JsonConvert.DeserializeObject<Map>().
		/// Safe to call multiple times (idempotent).
		/// </summary>
		public void NormalizeTableAfterLoad()
		{
			if (table == null || table.Length == 0)
				return;

			// Make sure internal list is at least as long as table
			while (_stableIds.Count < table.Length)
				_stableIds.Add(null);

			for (int i = 0; i < table.Length; i++)
			{
				string entry = table[i];
				if (string.IsNullOrWhiteSpace(entry))
				{
					table[i] = "tile_empty";
					_stableIds[i] = null;
					continue;
				}

				entry = entry.Trim();

				// Check for [hash] prefix
				if (entry.StartsWith("[") && entry.Contains("]"))
				{
					int closeBracket = entry.IndexOf(']');
					if (closeBracket > 1) // at least [x]
					{
						string hashPart = entry.Substring(1, closeBracket - 1).Trim();
						string namePart = entry.Substring(closeBracket + 1).Trim();

						// Only accept reasonable-looking hashes (e.g. alphanumeric, 4-12 chars)
						if (!string.IsNullOrEmpty(hashPart) && hashPart.Length >= 4 && hashPart.All(c => char.IsLetterOrDigit(c) || c == '-'))
						{
							_stableIds[i] = hashPart;
							table[i] = string.IsNullOrEmpty(namePart) ? "tile_empty" : namePart;
							continue;
						}
					}
				}

				// No valid prefix → treat as plain name
				_stableIds[i] = null;
				table[i] = entry;
			}

			// Trim any excess stable IDs (shouldn't happen, but safety)
			if (_stableIds.Count > table.Length)
				_stableIds.RemoveRange(table.Length, _stableIds.Count - table.Length);

			Debug.Log($"Normalized map table: {_stableIds.Count(s => !string.IsNullOrEmpty(s))} entries had hash prefixes");
		}

		// ─────────────────────────────────────────────────────────────────────
		// CLEAN, SAFE, RUNTIME-FRIENDLY ATTACHMENT MANAGEMENT
		// ─────────────────────────────────────────────────────────────────────
		public void AddAttachment(MapAttachment attachment)
		{
			if (attachment == null) return;

			var list = attachments?.ToList() ?? new List<MapAttachment>();
			list.Add(attachment);
			attachments = list.ToArray();
		}

		public bool RemoveAttachment(MapAttachment attachment)
		{
			if (attachment == null || attachments == null || attachments.Length == 0)
				return false;

			int index = Array.IndexOf(attachments, attachment);
			if (index < 0) return false;

			var list = attachments.ToList();
			list.RemoveAt(index);
			attachments = list.ToArray();
			return true;
		}

		public void RemoveAllAttachmentsOnTile(int tileIndex)
		{
			if (attachments == null || attachments.Length == 0 || tileIndex < 0)
				return;

			attachments = attachments.Where(a => a.tile != tileIndex).ToArray();
		}

		public MapAttachment[] GetAttachmentsOnTile(int tileIndex)
		{
			if (attachments == null || tileIndex < 0)
				return Array.Empty<MapAttachment>();

			return attachments.Where(a => a.tile == tileIndex).ToArray();
		}

		// in Map.cs
		public string GetDefinitionIdAt(int tileIndex)
		{
			if (tiles == null || table == null || tileIndex < 0 || tileIndex >= tiles.Length)
				return null;
			int idx = tiles[tileIndex];
			if (idx < 0 || idx >= table.Length) return null;
			return table[idx];
		}

		public bool SetDefinitionIdAt(int tileIndex, string newDefId)
		{
			if (tiles == null || table == null || tileIndex < 0 || tileIndex >= tiles.Length)
				return false;

			int idx = Array.IndexOf(table, newDefId);
			if (idx == -1)
			{
				var list = table.ToList();
				list.Add(newDefId);
				table = list.ToArray();
				idx = table.Length - 1;
			}

			tiles[tileIndex] = idx;
			return true;
		}

		// Atomic fields - ignored during normal serialization - this needs to be removed from here and implemented properly as a utility for the serialiser
		[JsonIgnore] public Definition[] definitions;
		[JsonIgnore] public TextureSequence[] textures;
		[JsonIgnore] public string version = "1.0";
		[JsonIgnore] public string author = "Player";
		[JsonIgnore] public string exportedFrom = "ClassicTilestorm"; 
		//[JsonIgnore] public bool IsAtomic => definitions?.Length > 0 || textures?.Length > 0;
	}
}