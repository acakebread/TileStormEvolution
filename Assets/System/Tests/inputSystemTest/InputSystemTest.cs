using UnityEngine;
using UnityEngine.InputSystem;

public class InputSystemTest : MonoBehaviour
{
	private string logText = "Input Test Started...\n";
	private Vector2 lastMousePos;
	private int frameCount = 0;

	private void Start()
	{
		Debug.Log("=== InputSystemTest Started ===");

		// Log initial device state
		LogDevices();

		lastMousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
	}

	private void Update()
	{
		frameCount++;

		// Keyboard test (polling several common keys)
		if (Keyboard.current != null)
		{
			if (Keyboard.current.anyKey.wasPressedThisFrame)
				AppendLog("Any key pressed!");

			if (Keyboard.current.escapeKey.wasPressedThisFrame)
				AppendLog("ESC pressed");

			if (Keyboard.current.spaceKey.wasPressedThisFrame)
				AppendLog("SPACE pressed");

			if (Keyboard.current.wKey.isPressed)
				AppendLog("W held");

			if (Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed)
				AppendLog("A/D held");
		}
		else
		{
			if (frameCount % 60 == 0)
				AppendLog("WARNING: Keyboard.current is NULL");
		}

		// Mouse test
		if (Mouse.current != null)
		{
			Vector2 pos = Mouse.current.position.ReadValue();
			if (Vector2.Distance(pos, lastMousePos) > 5f)
			{
				AppendLog($"Mouse moved: {pos}");
				lastMousePos = pos;
			}

			if (Mouse.current.leftButton.wasPressedThisFrame)
				AppendLog("Left Mouse Button DOWN");

			if (Mouse.current.rightButton.wasPressedThisFrame)
				AppendLog("Right Mouse Button DOWN");

			if (Mouse.current.scroll.ReadValue().y != 0)
				AppendLog($"Scroll: {Mouse.current.scroll.ReadValue().y}");
		}
		else
		{
			if (frameCount % 60 == 0)
				AppendLog("WARNING: Mouse.current is NULL");
		}

		// Show on-screen log (so you can see it in a built player without attaching the console)
		if (frameCount % 30 == 0)  // refresh every 0.5s
		{
			Debug.Log(logText);
		}
	}

	private void OnGUI()
	{
		// On-screen display for built player
		GUIStyle style = new GUIStyle(GUI.skin.label);
		style.fontSize = 18;
		style.normal.textColor = Color.white;

		GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
		GUILayout.Label(logText, style);
		GUILayout.EndArea();
	}

	private void AppendLog(string message)
	{
		logText += $"[{Time.frameCount}] {message}\n";

		// Keep only last 30 lines
		string[] lines = logText.Split('\n');
		if (lines.Length > 35)
			logText = string.Join("\n", lines, lines.Length - 35, 35);
	}

	private void LogDevices()
	{
		AppendLog($"Keyboard.current: {(Keyboard.current != null ? "OK" : "NULL")}");
		AppendLog($"Mouse.current: {(Mouse.current != null ? "OK" : "NULL")}");
		AppendLog($"Total devices: {InputSystem.devices.Count}");

		foreach (var device in InputSystem.devices)
		{
			AppendLog($"→ {device.displayName} ({device.GetType().Name})");
		}
	}

	// Optional: press R to reset log
	private void OnEnable()
	{
		if (Keyboard.current != null)
			Keyboard.current.onTextInput += OnTextInput;
	}

	private void OnDisable()
	{
		if (Keyboard.current != null)
			Keyboard.current.onTextInput -= OnTextInput;
	}

	private void OnTextInput(char ch)
	{
		AppendLog($"Text input: '{ch}'");
	}
}