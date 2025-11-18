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
		public string textureBank;
		public string flags;
		public string connections;
		public string pickup;           // only present if not None
		public bool isPuzzleBlock;

		// LEGACY COMPATIBILITY — NEVER SERIALIZED
		[JsonIgnore] public string szType => id ?? "";
		[JsonIgnore] public string szGeom => model ?? "";
		[JsonIgnore] public string szBank => textureBank ?? "Default";
		[JsonIgnore] public bool bNorth => HasConnection('N');
		[JsonIgnore] public bool bSouth => HasConnection('S');
		[JsonIgnore] public bool bEast => HasConnection('E');
		[JsonIgnore] public bool bWest => HasConnection('W');
		[JsonIgnore] public bool bStart => HasFlag("Start");
		[JsonIgnore] public bool bEnd => HasFlag("End");
		[JsonIgnore] public bool bConsole => HasFlag("Console");
		[JsonIgnore] public bool bSlide => HasFlag("Slide");
		[JsonIgnore] public bool bRoll => HasFlag("Roll");
		[JsonIgnore] public bool bDock => HasFlag("Dock");
		[JsonIgnore] public bool bDoor => HasFlag("Door");
		[JsonIgnore] public int nPickup => pickup switch { "Coin" => 1, "Key" => 2, "Health" => 3, "Ammo" => 4, _ => 0 };
		[JsonIgnore] public bool bPuzzleBlock => isPuzzleBlock;

		// helpers...
		private HashSet<string> _flagSet;
		public bool HasFlag(string flag) =>
			(_flagSet ??= new HashSet<string>((flags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant())))
			.Contains(flag.ToLowerInvariant());

		private HashSet<char> _connSet;
		public bool HasConnection(char dir) =>
			(_connSet ??= new HashSet<char>((connections ?? "").ToUpperInvariant()))
			.Contains(char.ToUpperInvariant(dir));
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