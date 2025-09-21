using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

public class CommandBufferSettings : MonoBehaviour
{
	private class CommandEntry
	{
		public Action<RasterCommandBuffer, Camera> Command;
		public string CameraName; // Null for all cameras
	}

	private readonly Dictionary<RenderPassEvent, List<CommandEntry>> commands = new();

	void Awake()
	{
		InitializeCommands();
	}

	private void InitializeCommands()
	{
		// Initialize dictionary for relevant RenderPassEvent values
		foreach (RenderPassEvent evt in Enum.GetValues(typeof(RenderPassEvent)))
		{
			if (!commands.ContainsKey(evt))
			{
				commands[evt] = new List<CommandEntry>();
			}
		}
	}

	public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command, string cameraName = null)
	{
		if (!commands.ContainsKey(evt))
		{
			commands[evt] = new List<CommandEntry>();
		}
		commands[evt].Add(new CommandEntry { Command = command, CameraName = cameraName });
	}

	public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
	{
		if (!HasCommands(evt))
			return;

		foreach (var entry in commands[evt])
		{
			if (entry.CameraName == null || entry.CameraName == camera.name)
			{
				try
				{
					entry.Command?.Invoke(commandBuffer, camera);
				}
				catch (Exception e)
				{
					Debug.LogError($"CommandBufferSettings: Error executing command for event {evt}, camera {camera.name}: {e.Message}");
				}
			}
		}
	}

	public bool HasCommands(RenderPassEvent evt)
	{
		return commands.ContainsKey(evt) && commands[evt].Count > 0;
	}

	void OnDestroy()
	{
		commands.Clear();
	}
}