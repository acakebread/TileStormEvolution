using System;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class CameraModeRegistry
	{
		// Define mode names as constants
		public const string Absent = "Absent";
		public const string Direct = "Direct";
		public const string Editor = "Editor";
		public const string Preset = "Preset";
		public const string Follow = "Follow";
		public const string Orbit = "Orbit";
		public const string Path = "Path";
	}

	public static class GameModes
	{
		// String constants for GameModes
		public const string Direct = "DIRECT";
		public const string Editor = "EDITOR";
		public const string Player = "PLAYER";
		public const string Cinema = "CINEMA";

		// Mapping from PreviewMode to (GameMode string, CameraModeRegistry array)
		private static readonly Dictionary<PreviewMode, (string Mode, string[] CameraModes)> ModeMap = new()
		{
			{ PreviewMode.Direct, (Direct, new[] { CameraModeRegistry.Direct }) },
			{ PreviewMode.Editor, (Editor, new[] { CameraModeRegistry.Editor }) },
			{ PreviewMode.Player, (Player, new[] { CameraModeRegistry.Follow, CameraModeRegistry.Preset }) },
			{ PreviewMode.Cinema, (Cinema, new[] { CameraModeRegistry.Path, CameraModeRegistry.Orbit }) }
		};

		// Methods to access the mapping
		public static string GetModeString(PreviewMode mode) => ModeMap.TryGetValue(mode, out var value) ? value.Mode : CameraModeRegistry.Absent;
		public static string[] GetCameraModes(PreviewMode mode) => ModeMap.TryGetValue(mode, out var value) ? value.CameraModes : Array.Empty<string>();

		// Method to register all modes (used in MainCameraController)
		public static void RegisterAllModes(Action<string, string[]> registerAction)
		{
			foreach (var entry in ModeMap.Values)
			{
				registerAction(entry.Mode, entry.CameraModes);
			}
		}
	}
}