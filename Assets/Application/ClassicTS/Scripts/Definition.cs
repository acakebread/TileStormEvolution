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
		public string flags;         // comma/space separated flags, e.g. "Drag, Roll, Dock"
		public string connections;   // e.g. "NSEW"

		// ── CONNECTIONS (read-only, as before) ─────────────────────────────────
		[JsonIgnore] public bool bNorth => HasConnection('N');
		[JsonIgnore] public bool bSouth => HasConnection('S');
		[JsonIgnore] public bool bEast => HasConnection('E');
		[JsonIgnore] public bool bWest => HasConnection('W');

		// ── FLAGS (now settable – changes affect the 'flags' string) ───────────
		[JsonIgnore]
		public bool bDrag
		{
			get => HasFlag("Drag");
			set => SetFlag("Drag", value);
		}

		[JsonIgnore]
		public bool bRoll
		{
			get => HasFlag("Roll");
			set => SetFlag("Roll", value);
		}

		[JsonIgnore]
		public bool bDock
		{
			get => HasFlag("Dock");
			set => SetFlag("Dock", value);
		}

		[JsonIgnore]
		public bool bDoor
		{
			get => HasFlag("Door");
			set => SetFlag("Door", value);
		}

		[JsonIgnore]
		public bool bStart
		{
			get => HasFlag("Start");
			set => SetFlag("Start", value);
		}

		[JsonIgnore]
		public bool bEnd
		{
			get => HasFlag("End");
			set => SetFlag("End", value);
		}

		[JsonIgnore]
		public bool bConsole
		{
			get => HasFlag("Console");
			set => SetFlag("Console", value);
		}

		[JsonIgnore]
		public bool bPuzzleBlock
		{
			get => HasFlag("PuzzleBlock");
			set => SetFlag("PuzzleBlock", value);
		}

		[JsonIgnore]
		public bool bSway
		{
			get => HasFlag("Sway");
			set => SetFlag("Sway", value);
		}

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
			{
				_connCache = new HashSet<char>();
				if (!string.IsNullOrEmpty(connections))
				{
					foreach (char c in connections.ToUpperInvariant())
						_connCache.Add(c);
				}
			}
			return _connCache.Contains(char.ToUpperInvariant(dir));
		}

		// Called whenever we modify the flags string externally or via properties
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

		// Core method used by all boolean setters
		private void SetFlag(string flag, bool enabled)
		{
			RebuildFlagCache(); // ensure cache is up-to-date first

			bool currentlyHas = _flagCache.Contains(flag, StringComparer.OrdinalIgnoreCase);

			if (enabled && !currentlyHas)
			{
				// Add the flag
				if (string.IsNullOrEmpty(flags))
					flags = flag;
				else
					flags += $", {flag}";
			}
			else if (!enabled && currentlyHas)
			{
				// Remove the flag
				_flagCache.Remove(flag); // update cache early

				var flagList = flags
					.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim())
					.Where(f => !string.Equals(f, flag, StringComparison.OrdinalIgnoreCase));

				flags = string.Join(", ", flagList);
			}

			// Cache is already updated above, but if someone accesses flags directly later,
			// we ensure consistency on next HasFlag() call.
		}
	}

	// You can keep the extension methods if you want, but the instance methods above
	// are now more efficient and consistent with the mutable design.
	public static class DefinitionExtensions
	{
		public static bool HasConnection(this Definition def, char dir) =>
			def?.connections?.IndexOf(dir, StringComparison.OrdinalIgnoreCase) >= 0;
	}
}

//using System;
//using System.Linq;
//using System.Collections.Generic;
//using Newtonsoft.Json;

//namespace ClassicTilestorm
//{
//	[Serializable]
//	public class Definition
//	{
//		public string id;
//		public string model;
//		public string texture;
//		public string material;
//		public string flags;
//		public string connections;

//		[JsonIgnore] public bool bNorth => HasConnection('N');
//		[JsonIgnore] public bool bSouth => HasConnection('S');
//		[JsonIgnore] public bool bEast => HasConnection('E');
//		[JsonIgnore] public bool bWest => HasConnection('W');

//		[JsonIgnore] public bool bDrag => HasFlag("Drag");
//		[JsonIgnore] public bool bRoll => HasFlag("Roll");
//		[JsonIgnore] public bool bDock => HasFlag("Dock");

//		[JsonIgnore] public bool bDoor => HasFlag("Door");//door flag should replace start and end which should be stored with the waypoints instead
//		[JsonIgnore] public bool bStart => HasFlag("Start");
//		[JsonIgnore] public bool bEnd => HasFlag("End");
//		[JsonIgnore] public bool bConsole => HasFlag("Console");

//		[JsonIgnore] public bool bPuzzleBlock => HasFlag("PuzzleBlock");

//		//extra properties
//		[JsonIgnore] public bool bSway => HasFlag("Sway");//Sway means reacts to wind

//		public bool ShouldSerializetexture() => !string.IsNullOrEmpty(texture);
//		public bool ShouldSerializematerial() => !string.IsNullOrEmpty(material);

//		// ── INTERNAL HELPERS ───────────────────────────────────────────────────
//		private HashSet<string> _flagCache;
//		public bool HasFlag(string flag)
//		{
//			if (_flagCache == null)
//			{
//				_flagCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
//				if (!string.IsNullOrEmpty(flags))
//				{
//					foreach (var f in flags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
//						_flagCache.Add(f.Trim());
//				}
//			}
//			return _flagCache.Contains(flag);
//		}

//		private HashSet<char> _connCache;
//		public bool HasConnection(char dir)
//		{
//			if (_connCache == null)
//			{
//				_connCache = new HashSet<char>();
//				if (!string.IsNullOrEmpty(connections))
//					foreach (char c in connections.ToUpperInvariant())
//						_connCache.Add(c);
//			}
//			return _connCache.Contains(char.ToUpperInvariant(dir));
//		}
//	}

//	public static class DefinitionExtensions
//	{
//		private static HashSet<string> GetFlags(this Definition def) =>
//			def?.flags?.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
//					  .Select(s => s.Trim())
//					  .ToHashSet() ?? new HashSet<string>();

//		public static bool HasFlag(this Definition def, string flag) => def.GetFlags().Contains(flag);
//		public static bool HasConnection(this Definition def, char dir) =>
//			def?.connections?.Contains(dir.ToString(), StringComparison.OrdinalIgnoreCase) == true;
//	}
//}