using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[Serializable]
	public class Map
	{
		[Serializable]
		public class TileEntry
		{
			public string StableId;
			public string DisplayName;

			public TileEntry(string displayName, string stableId = null)
			{
				DisplayName = displayName ?? "undefined"; //DisplayName = displayName ?? "tile_empty";
				StableId = stableId;
			}
		}

		// ─────────────────────────────────────────────
		// Core identity
		// ─────────────────────────────────────────────
		[JsonProperty(Order = 1)] public string name;
		[JsonProperty(Order = 2)] public string character;
		[JsonProperty(Order = 3)] public string music;
		[JsonProperty(Order = 4)] public string skybox;
		[JsonProperty(Order = 5)] public string button;

		// ─────────────────────────────────────────────
		// Dimensions
		// ─────────────────────────────────────────────
		[JsonProperty(Order = 10)] public int width;
		[JsonProperty(Order = 11)] public int height;

		// ─────────────────────────────────────────────
		// Tile table backing store
		// ─────────────────────────────────────────────
		[JsonIgnore]
		internal List<TileEntry> _tileEntries = new List<TileEntry>();

		[JsonProperty(Order = 20)]
		public string[] table
		{
			get => _tileEntries?.Select(e => e.DisplayName).ToArray() ?? Array.Empty<string>();
			set
			{
				_tileEntries.Clear();
				if (value == null) return;
				foreach (string name in value)
					_tileEntries.Add(new TileEntry(name));
			}
		}

		[JsonProperty(Order = 21)] public int[] tiles;
		[JsonProperty(Order = 22)] public int[] solve;
		[JsonProperty(Order = 23)] public int[] waypoints;

		[JsonProperty(Order = 30)] public MapAttachment[] attachments;

		// ─────────────────────────────────────────────
		// ATOMIC-ONLY FIELDS (ORDERED, NOT IGNORED)
		// ─────────────────────────────────────────────
		[JsonProperty(Order = 100)] public Definition[] definitions;
		[JsonProperty(Order = 101)] public TextureSequence[] textures;
		[JsonProperty(Order = 102)] public string version = "1.0";
		[JsonProperty(Order = 103)] public string author = "Player";
		[JsonProperty(Order = 104)] public string exportedFrom = "ClassicTilestorm";

		// ─────────────────────────────────────────────
		// Conditional serialization
		// ─────────────────────────────────────────────
		public bool ShouldSerializeskybox() => !string.IsNullOrEmpty(skybox);
		public bool ShouldSerializesolve() => solve != null && solve.Length > 0;
		public bool ShouldSerializewaypoints() => waypoints != null && waypoints.Length > 0;
		public bool ShouldSerializeattachments() => attachments != null && attachments.Length > 0;

		public bool IsValidTile(int index) => index >= 0 && index < width * height;

		public enum Anchor
		{
			TopLeft, TopCenter, TopRight,
			MiddleLeft, Center, MiddleRight,
			BottomLeft, BottomCenter, BottomRight
		}

		public bool Consolidate()
		{
			if (tiles == null || tiles.Length == 0) return false;

			// Keep a map of name → best known stableId
			var nameToStable = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (var entry in _tileEntries)
			{
				if (!string.IsNullOrEmpty(entry.DisplayName) &&
					!string.IsNullOrEmpty(entry.StableId) &&
					!nameToStable.ContainsKey(entry.DisplayName))
				{
					nameToStable[entry.DisplayName] = entry.StableId;
				}
			}

			var mapDefinitions = tiles.Select(idx =>
				(idx >= 0 && idx < table.Length) ? table[idx] ?? "tile_empty" : "tile_empty"
			).ToArray();

			var newFrequencyTable = mapDefinitions.ToFrequencySortedTable();

			bool changed = !table.SequenceEqual(newFrequencyTable);

			if (changed)
			{
				var newEntries = new List<TileEntry>();
				foreach (string name in newFrequencyTable)
				{
					nameToStable.TryGetValue(name, out string stable);
					newEntries.Add(new TileEntry(name, stable));
				}
				_tileEntries = newEntries;
			}

			// Rebuild tiles array with new indices
			if (changed)
			{
				tiles = mapDefinitions.Select(name => Array.IndexOf(table, name)).ToArray();
			}

			if (changed) Debug.Log($"{name} consolidated (stable IDs preserved where known)");
			return changed;
		}

		public bool Resize(int newWidth, int newHeight, Anchor anchor = Anchor.Center)
		{
			if (newWidth <= 0 || newHeight <= 0) return false;
			if (width == newWidth && height == newHeight) return true;

			int oldWidth = width;
			int oldHeight = height;

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

			bool success = RepositionAndResize(newWidth, newHeight, offsetX, offsetZ);

			if (success) Consolidate();

			if (success)
				Debug.Log($"Map '{name}' resized to {newWidth}x{newHeight} (anchor: {anchor}).");

			return success;
		}

		public bool RepositionAndResize(int newWidth, int newHeight, int offsetX, int offsetZ)
		{
			if (newWidth <= 0 || newHeight <= 0) return false;

			int oldWidth = width;
			int oldHeight = height;
			int newSize = newWidth * newHeight;

			int emptyIndex = table.Contains("tile_empty") ? Array.IndexOf(table, "tile_empty") : -1;
			if (emptyIndex == -1)
			{
				var list = table.ToList();
				list.Add("tile_empty");
				table = list.ToArray();
				emptyIndex = table.Length - 1;
			}

			var newTiles = new int[newSize];
			Array.Fill(newTiles, emptyIndex);

			for (int z = 0; z < oldHeight; z++)
				for (int x = 0; x < oldWidth; x++)
				{
					int oldIdx = z * oldWidth + x;
					if (oldIdx >= tiles.Length) continue;

					int nx = x + offsetX;
					int nz = z + offsetZ;

					if (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight)
						newTiles[nz * newWidth + nx] = tiles[oldIdx];
				}

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

			int Remap(int idx)
			{
				if (idx < 0) return idx;
				int x = idx % oldWidth;
				int z = idx / oldWidth;
				int nx = x + offsetX;
				int nz = z + offsetZ;
				return (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight) ? nz * newWidth + nx : -1;
			}

			if (waypoints != null)
				for (int n = 0; n < waypoints.Length; n++) waypoints[n] = Remap(waypoints[n]);

			if (attachments != null)
				foreach (var a in attachments) a.tile = Remap(a.tile);

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

			if (success) Consolidate();

			return success;
		}

		public (int minX, int minZ, int maxX, int maxZ) GetContentBounds()
		{
			if (tiles == null || tiles.Length == 0 || width <= 0 || height <= 0)
				return (0, 0, -1, -1);

			int minX = width;
			int minZ = height;
			int maxX = -1;
			int maxZ = -1;

			int emptyIdx = table.Contains("tile_empty") ? Array.IndexOf(table, "tile_empty") : -1;

			for (int i = 0; i < tiles.Length; i++)
			{
				int t = tiles[i];
				if (t < 0 || t == emptyIdx || (t < table.Length && table[t] == "tile_empty"))
					continue;

				int x = i % width;
				int z = i / width;

				minX = Math.Min(minX, x);
				maxX = Math.Max(maxX, x);
				minZ = Math.Min(minZ, z);
				maxZ = Math.Max(maxZ, z);
			}

			return maxX >= 0 ? (minX, minZ, maxX, maxZ) : (0, 0, -1, -1);
		}

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

				waypoints = waypoints != null ? (int[])waypoints.Clone() : null,
				tiles = tiles != null ? (int[])tiles.Clone() : null,
				solve = solve != null ? (int[])solve.Clone() : null,

				attachments = attachments != null ? attachments.Select(a => a.ShallowClone()).ToArray() : Array.Empty<MapAttachment>()
			};

			copy._tileEntries = _tileEntries != null
				? new List<TileEntry>(_tileEntries.Select(e => new TileEntry(e.DisplayName, e.StableId)))
				: new List<TileEntry>();

			bool cropped = copy.CropToContent();

			if (cropped)
				Debug.Log($"[Export] Map '{copy.name}' auto-cropped to {copy.width}x{copy.height}");

			return copy;
		}

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
	}
}