using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

public class CommandBufferSettingsRG : MonoBehaviour
{
	public enum RenderPassMode
	{
		BeforeRendering,
		BeforeRenderingOpaques,
		AfterRenderingTransparents,
		AfterRendering
	}

	private class CommandEntry
	{
		public Action<RasterCommandBuffer, Camera> Command;
		public string CameraName; // Null for all cameras
	}

	private readonly Dictionary<RenderPassMode, List<CommandEntry>> commands = new Dictionary<RenderPassMode, List<CommandEntry>>();

	void Awake()
	{
		InitializeCommands();
	}

	private void InitializeCommands()
	{
		// Ensure all RenderPassMode keys are initialized
		foreach (RenderPassMode mode in Enum.GetValues(typeof(RenderPassMode)))
		{
			if (!commands.ContainsKey(mode))
			{
				commands[mode] = new List<CommandEntry>();
			}
		}
	}

	public void RegisterCommand(RenderPassMode mode, Action<RasterCommandBuffer, Camera> command, string cameraName = null)
	{
		// Lazy initialization for the mode if not already present
		if (!commands.ContainsKey(mode))
		{
			commands[mode] = new List<CommandEntry>();
		}
		commands[mode].Add(new CommandEntry { Command = command, CameraName = cameraName });
	}

	public void ExecuteCommands(RenderPassMode mode, RasterCommandBuffer commandBuffer, Camera camera)
	{
		if (!HasCommands(mode))
			return;

		foreach (var entry in commands[mode])
		{
			if (entry.CameraName == null || entry.CameraName == camera.name)
			{
				try { entry.Command?.Invoke(commandBuffer, camera); }
				catch (Exception e)
				{
					Debug.LogError($"CommandBufferSettingsRG: Error executing command for mode {mode}, camera {camera.name}: {e.Message}");
				}
			}
		}
	}

	public bool HasCommands(RenderPassMode mode)
	{
		return commands.ContainsKey(mode) && commands[mode].Count > 0;
	}

	void OnDestroy()
	{
		commands.Clear();
	}
}