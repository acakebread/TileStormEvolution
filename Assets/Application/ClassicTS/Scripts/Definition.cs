using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[Serializable]
	public class Definition
	{
		public string id;
		public string model;
		public string texture;
		public string material;
		public string flags;         // comma/space separated, e.g. "Drag, Roll, Dock"
		public string connections;   // e.g. "NSEW" (uppercase, no separators)

		// ── CONNECTIONS (now settable – changes affect the 'connections' string) ──
		[JsonIgnore]
		public bool bNorth
		{
			get => HasConnection('N');
			set => SetConnection('N', value);
		}

		[JsonIgnore]
		public bool bSouth
		{
			get => HasConnection('S');
			set => SetConnection('S', value);
		}

		[JsonIgnore]
		public bool bEast
		{
			get => HasConnection('E');
			set => SetConnection('E', value);
		}

		[JsonIgnore]
		public bool bWest
		{
			get => HasConnection('W');
			set => SetConnection('W', value);
		}

		// ── FLAGS (settable, as before) ───────────────────────────────────────
		[JsonIgnore] public bool bDrag { get => HasFlag("Drag"); set => SetFlag("Drag", value); }
		[JsonIgnore] public bool bRoll { get => HasFlag("Roll"); set => SetFlag("Roll", value); }
		[JsonIgnore] public bool bDock { get => HasFlag("Dock"); set => SetFlag("Dock", value); }
		[JsonIgnore] public bool bDoor { get => HasFlag("Door"); set => SetFlag("Door", value); }
		[JsonIgnore] public bool bStart { get => HasFlag("Start"); set => SetFlag("Start", value); }
		[JsonIgnore] public bool bEnd { get => HasFlag("End"); set => SetFlag("End", value); }
		[JsonIgnore] public bool bConsole { get => HasFlag("Console"); set => SetFlag("Console", value); }
		[JsonIgnore] public bool bPuzzleBlock { get => HasFlag("PuzzleBlock"); set => SetFlag("PuzzleBlock", value); }
		[JsonIgnore] public bool bSway { get => HasFlag("Sway"); set => SetFlag("Sway", value); }
		[JsonIgnore] public bool bWash { get => HasFlag("Wash"); set => SetFlag("Wash", value); }

		// ── CONDITIONAL SERIALIZATION ─────────────────────────────────────────
		public bool ShouldSerializetexture() => !string.IsNullOrEmpty(texture);
		public bool ShouldSerializematerial() => !string.IsNullOrEmpty(material);

		// ── INTERNAL HELPERS ──────────────────────────────────────────────────
		private HashSet<string> _flagCache;
		private HashSet<char> _connCache;

		public bool HasFlag(string flag)
		{
			if (_flagCache == null)
				RebuildFlagCache();

			return _flagCache.Contains(flag, StringComparer.OrdinalIgnoreCase);
		}

		public bool HasConnection(char dir)
		{
			if (_connCache == null)
				RebuildConnectionCache();

			return _connCache.Contains(char.ToUpperInvariant(dir));
		}

		private void RebuildFlagCache()
		{
			_flagCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (!string.IsNullOrEmpty(flags))
			{
				foreach (var f in flags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var trimmed = f.Trim();
					if (!string.IsNullOrEmpty(trimmed))
						_flagCache.Add(trimmed);
				}
			}
		}

		private void RebuildConnectionCache()
		{
			_connCache = new HashSet<char>();

			if (!string.IsNullOrEmpty(connections))
			{
				foreach (char c in connections)
				{
					if (char.IsLetter(c))
						_connCache.Add(char.ToUpperInvariant(c));
				}
			}
		}

		// ── FLAG MUTATION ─────────────────────────────────────────────────────
		private void SetFlag(string flag, bool enabled)
		{
			if (_flagCache == null)
				RebuildFlagCache();

			bool currentlyHas = _flagCache.Contains(flag, StringComparer.OrdinalIgnoreCase);

			if (enabled == currentlyHas) return;

			var flagList = string.IsNullOrEmpty(flags)
				? new List<string>()
				: flags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
					   .Select(s => s.Trim())
					   .Where(f => !string.Equals(f, flag, StringComparison.OrdinalIgnoreCase))
					   .ToList();

			if (enabled)
				flagList.Add(flag);

			flags = string.Join(", ", flagList);
			RebuildFlagCache();
		}

		// ── CONNECTION MUTATION ───────────────────────────────────────────────
		private void SetConnection(char dirChar, bool enabled)
		{
			char dir = char.ToUpperInvariant(dirChar);

			if (_connCache == null)
				RebuildConnectionCache();

			bool currentlyHas = _connCache.Contains(dir);

			if (enabled == currentlyHas) return;

			var connList = new List<char>();

			if (!string.IsNullOrEmpty(connections))
			{
				foreach (char c in connections)
				{
					if (char.IsLetter(c) && char.ToUpperInvariant(c) != dir)
						connList.Add(char.ToUpperInvariant(c));
				}
			}

			if (enabled)
				connList.Add(dir);

			// Sort for consistency (optional, but nice: N, E, S, W)
			connList.Sort();

			connections = new string(connList.ToArray());
			RebuildConnectionCache();
		}
	}

	// Optional extension (not needed anymore for core functionality)
	public static class DefinitionExtensions
	{
		// Kept for backward compatibility if used elsewhere
		public static bool HasConnection(this Definition def, char dir) =>
			def?.HasConnection(dir) ?? false;
	}
}