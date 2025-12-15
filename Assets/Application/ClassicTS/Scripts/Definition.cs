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
		public string flags;
		public string connections;

		[JsonIgnore] public bool bNorth => HasConnection('N');
		[JsonIgnore] public bool bSouth => HasConnection('S');
		[JsonIgnore] public bool bEast => HasConnection('E');
		[JsonIgnore] public bool bWest => HasConnection('W');

		[JsonIgnore] public bool bDrag => HasFlag("Drag");
		[JsonIgnore] public bool bRoll => HasFlag("Roll");
		[JsonIgnore] public bool bDock => HasFlag("Dock");

		[JsonIgnore] public bool bDoor => HasFlag("Door");//door flag should replace start and end which should be stored with the waypoints instead
		[JsonIgnore] public bool bStart => HasFlag("Start");
		[JsonIgnore] public bool bEnd => HasFlag("End");
		[JsonIgnore] public bool bConsole => HasFlag("Console");

		[JsonIgnore] public bool bPuzzleBlock => HasFlag("PuzzleBlock");

		public bool ShouldSerializetexture() => !string.IsNullOrEmpty(texture);
		public bool ShouldSerializematerial() => !string.IsNullOrEmpty(material);

		// ── INTERNAL HELPERS ───────────────────────────────────────────────────
		private HashSet<string> _flagCache;
		public bool HasFlag(string flag)
		{
			if (_flagCache == null)
			{
				_flagCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				if (!string.IsNullOrEmpty(flags))
				{
					foreach (var f in flags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
						_flagCache.Add(f.Trim());
				}
			}
			return _flagCache.Contains(flag);
		}

		private HashSet<char> _connCache;
		public bool HasConnection(char dir)
		{
			if (_connCache == null)
			{
				_connCache = new HashSet<char>();
				if (!string.IsNullOrEmpty(connections))
					foreach (char c in connections.ToUpperInvariant())
						_connCache.Add(c);
			}
			return _connCache.Contains(char.ToUpperInvariant(dir));
		}
	}

	public static class DefinitionExtensions
	{
		private static HashSet<string> GetFlags(this Definition def) =>
			def?.flags?.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
					  .Select(s => s.Trim())
					  .ToHashSet() ?? new HashSet<string>();

		public static bool HasFlag(this Definition def, string flag) => def.GetFlags().Contains(flag);
		public static bool HasConnection(this Definition def, char dir) =>
			def?.connections?.Contains(dir.ToString(), StringComparison.OrdinalIgnoreCase) == true;
	}
}