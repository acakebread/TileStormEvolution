using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[Serializable]
	public class Map
	{
		[Serializable]
		public class TileEntry  // ← change from internal to public
		{
			public string StableId;
			public string DisplayName;

			public TileEntry(string displayName, string stableId = null)
			{
				DisplayName = displayName ?? "tile_empty";
				StableId = stableId;
			}
		}

		public string name;
		public string character;
		public string music;
		public string skybox;
		public string button;
		public int width;
		public int height;

		public int[] waypoints;


		[JsonIgnore]
		internal List<TileEntry> _tileEntries = new List<TileEntry>();

		public string[] table
		{
			get => _tileEntries?.Select(e => e.DisplayName).ToArray() ?? Array.Empty<string>();

			set
			{
				_tileEntries.Clear();
				if (value != null)
				{
					foreach (string name in value)
						_tileEntries.Add(new TileEntry(name));
				}
			}
		}

		public int[] tiles;
		public int[] solve;

		public MapAttachment[] attachments = Array.Empty<MapAttachment>();

		public void SetTileTypeAtIndex(int index, string displayName, string stableId = null)
		{
			while (_tileEntries.Count <= index)
				_tileEntries.Add(new TileEntry("tile_empty"));

			_tileEntries[index] = new TileEntry(displayName ?? "tile_empty", stableId);
		}

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
			if (tiles == null || tiles.Length == 0)
				return false;

			var nameToStable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < _tileEntries.Count; i++)
			{
				var entry = _tileEntries[i];
				if (!string.IsNullOrEmpty(entry.DisplayName) && !string.IsNullOrEmpty(entry.StableId))
				{
					if (!nameToStable.ContainsKey(entry.DisplayName))
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
				var newEntries = new List<TileEntry>(newFrequencyTable.Length);
				foreach (string name in newFrequencyTable)
				{
					nameToStable.TryGetValue(name, out string stable);
					newEntries.Add(new TileEntry(name, stable));
				}
				_tileEntries = newEntries;
			}

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

			if (changed) Debug.Log($"{name} consolidated");
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

		public string GetDefinitionIdAt(int tileIndex)
		{
			if (tiles == null || tileIndex < 0 || tileIndex >= tiles.Length)
				return null;

			int idx = tiles[tileIndex];
			if (idx < 0 || idx >= table.Length) return null;
			return table[idx];
		}

		public bool SetDefinitionIdAt(int tileIndex, string newDefId)
		{
			if (tiles == null || tileIndex < 0 || tileIndex >= tiles.Length)
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

		[JsonIgnore] public Definition[] definitions;
		[JsonIgnore] public TextureSequence[] textures;
		[JsonIgnore] public string version = "1.0";
		[JsonIgnore] public string author = "Player";
		[JsonIgnore] public string exportedFrom = "ClassicTilestorm";

		[JsonIgnore]
		public bool ExportEnrichedTable { get; set; } = false;  // false = normal database mode (hash only)
	}

	public abstract class MapConverterBase : JsonConverter
	{
		public override bool CanConvert(Type objectType) => typeof(Map).IsAssignableFrom(objectType);

		protected void ParseTable(Map map, JArray tableArray)
		{
			map._tileEntries.Clear();

			foreach (JToken token in tableArray)
			{
				string entry = token.Value<string>()?.Trim() ?? "tile_empty";
				string stableId = null;
				string displayName = entry;  // default to full entry

				if (entry.StartsWith("[", StringComparison.Ordinal))
				{
					int close = entry.IndexOf(']', 1);
					if (close > 1)  // found a closing bracket
					{
						string hashAndOptions = entry.Substring(1, close - 1).Trim();
						string namePart = (close + 1 < entry.Length) ? entry.Substring(close + 1).Trim() : "";

						// Take hash as first part before any comma (for future overrides)
						string hashPart = hashAndOptions.Split(',')[0].Trim();

						if (!string.IsNullOrEmpty(hashPart))
						{
							stableId = hashPart;
							displayName = string.IsNullOrWhiteSpace(namePart) ? "PENDING_ID" : namePart;
						}
					}
				}

				map._tileEntries.Add(new Map.TileEntry(displayName, stableId));
			}
		}

		protected virtual string ProcessRest(string rest)
		{
			return string.IsNullOrWhiteSpace(rest) ? "tile_empty" : rest;
		}

		protected abstract string GetOutput(Map.TileEntry entry, bool enriched);
	}

	public class AtomicMapConverter : MapConverterBase
	{
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;

			var jo = JObject.Load(reader);
			var map = (Map)(existingValue ?? Activator.CreateInstance(objectType));

			serializer.Populate(jo.CreateReader(), map);

			if (jo["table"] is JArray tableArray && tableArray.Count > 0)
			{
				ParseTable(map, tableArray);
			}

			return map;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value;
			writer.WriteStartObject();

			var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(typeof(Map));
			foreach (var prop in contract.Properties)
			{
				if (prop.Ignored || prop.PropertyName == "table") continue;

				var propValue = prop.ValueProvider?.GetValue(map);
				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore) continue;

				writer.WritePropertyName(prop.PropertyName ?? prop.UnderlyingName);
				serializer.Serialize(writer, propValue);
			}

			writer.WritePropertyName("table");
			writer.WriteStartArray();

			foreach (var entry in map._tileEntries)
			{
				string output = GetOutput(entry, true); // always enriched for atomic
				writer.WriteValue(output);
			}

			writer.WriteEndArray();
			writer.WriteEndObject();
		}

		protected override string GetOutput(Map.TileEntry entry, bool enriched)
		{
			// Atomic mode: ALWAYS include name if StableId exists
			if (!string.IsNullOrEmpty(entry.StableId))
			{
				return $"[{entry.StableId}]{entry.DisplayName}";
			}

			return entry.DisplayName;
		}
	}

	public class DatabaseMapConverter : MapConverterBase
	{
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;

			var jo = JObject.Load(reader);
			var map = (Map)(existingValue ?? Activator.CreateInstance(objectType));

			serializer.Populate(jo.CreateReader(), map);

			if (jo["table"] is JArray tableArray && tableArray.Count > 0)
			{
				ParseTable(map, tableArray);
			}

			return map;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value;
			writer.WriteStartObject();

			var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(typeof(Map));
			foreach (var prop in contract.Properties)
			{
				if (prop.Ignored || prop.PropertyName == "table") continue;

				var propValue = prop.ValueProvider?.GetValue(map);
				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore) continue;

				writer.WritePropertyName(prop.PropertyName ?? prop.UnderlyingName);
				serializer.Serialize(writer, propValue);
			}

			writer.WritePropertyName("table");
			writer.WriteStartArray();

			foreach (var entry in map._tileEntries)
			{
				string output = GetOutput(entry, false); // database mode: [hash] only
				writer.WriteValue(output);
			}

			writer.WriteEndArray();
			writer.WriteEndObject();
		}

		protected override string GetOutput(Map.TileEntry entry, bool enriched)
		{
			return !string.IsNullOrEmpty(entry.StableId)
				? $"[{entry.StableId}]"
				: entry.DisplayName;
		}

		protected override string ProcessRest(string rest)
		{
			return "tile_empty"; // no name in database mode
		}
	}

	//public class MapConverter : JsonConverter
	//{
	//	public override bool CanConvert(Type objectType)
	//	{
	//		return typeof(Map).IsAssignableFrom(objectType);
	//	}

	//	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	//	{
	//		if (reader.TokenType == JsonToken.Null) return null;

	//		var jo = JObject.Load(reader);
	//		var map = (Map)(existingValue ?? Activator.CreateInstance(objectType));

	//		serializer.Populate(jo.CreateReader(), map);

	//		if (jo["table"] is JArray tableArray && tableArray.Count > 0)
	//		{
	//			map._tileEntries.Clear();

	//			foreach (JToken token in tableArray)
	//			{
	//				string entry = token.Value<string>()?.Trim() ?? "tile_empty";
	//				string stableId = null;
	//				string displayName = entry;

	//				if (entry.StartsWith("[", StringComparison.Ordinal) && entry.EndsWith("]"))
	//				{
	//					// Bracketed hash (database save): [hash]
	//					string hashPart = entry.Substring(1, entry.Length - 2).Trim();
	//					if (!string.IsNullOrEmpty(hashPart))
	//					{
	//						stableId = hashPart;
	//						displayName = "tile_empty"; // temporary placeholder — fixed up next
	//					}
	//				}
	//				else if (entry.StartsWith("[", StringComparison.Ordinal) && entry.IndexOf(']') > 0)
	//				{
	//					// Full enriched (atomic): [hash]name
	//					int close = entry.IndexOf(']');
	//					string hashPart = entry.Substring(1, close - 1).Trim();
	//					string namePart = entry.Substring(close + 1).Trim();

	//					if (!string.IsNullOrEmpty(hashPart))
	//					{
	//						stableId = hashPart;
	//						displayName = string.IsNullOrWhiteSpace(namePart) ? "tile_empty" : namePart;
	//					}
	//				}
	//				// else: plain name (legacy)

	//				map._tileEntries.Add(new Map.TileEntry(displayName, stableId));
	//			}
	//		}

	//		return map;
	//	}

	//	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	//	{
	//		if (value == null)
	//		{
	//			writer.WriteNull();
	//			return;
	//		}

	//		var map = (Map)value;

	//		writer.WriteStartObject();

	//		var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(typeof(Map));
	//		foreach (var prop in contract.Properties)
	//		{
	//			if (prop.Ignored || prop.PropertyName == "table")
	//				continue;

	//			var propValue = prop.ValueProvider?.GetValue(map);
	//			if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
	//				continue;

	//			writer.WritePropertyName(prop.PropertyName ?? prop.UnderlyingName);
	//			serializer.Serialize(writer, propValue);
	//		}

	//		writer.WritePropertyName("table");
	//		writer.WriteStartArray();

	//		if (map._tileEntries != null && map._tileEntries.Count > 0)
	//		{
	//			foreach (var entry in map._tileEntries)
	//			{
	//				string output;

	//				if (map.ExportEnrichedTable)
	//				{
	//					output = string.IsNullOrEmpty(entry.StableId)
	//						? entry.DisplayName
	//						: $"[{entry.StableId}]{entry.DisplayName}";
	//				}
	//				else
	//				{
	//					output = !string.IsNullOrEmpty(entry.StableId)
	//						? $"[{entry.StableId}]"  // bracketed hash for database save
	//						: entry.DisplayName;
	//				}

	//				writer.WriteValue(output);
	//			}
	//		}

	//		writer.WriteEndArray();

	//		writer.WriteEndObject();
	//	}
}
