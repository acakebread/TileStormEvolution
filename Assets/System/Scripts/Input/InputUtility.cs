using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class InputUtility
	{
		private class KeyState
		{
			public float HeldTime { get; set; }
			public bool IsRepeating { get; set; }
			public bool HasRepeated { get; set; }
		}

		private static readonly Dictionary<KeyCode, KeyState> keyStates = new Dictionary<KeyCode, KeyState>();
		private static readonly float initialKeyDelay = 0.5f; // Delay before first repeat (seconds)
		private static readonly float repeatKeyInterval = 0.05f; // Interval between subsequent repeats (seconds)

		/// <summary>
		/// Checks if a key should trigger an action (initial press or repeat).
		/// Returns true on initial press or when repeat conditions are met.
		/// </summary>
		public static bool GetKeyRepeat(KeyCode key)
		{
			if (!InputX.GetKey(key))
			{
				if (InputX.GetKeyUp(key))
				{
					// Reset state on key release
					if (keyStates.ContainsKey(key))
					{
						keyStates.Remove(key);
					}
				}
				return false;
			}

			// Initialize key state if not present
			if (!keyStates.ContainsKey(key))
			{
				keyStates[key] = new KeyState { HeldTime = Time.time, IsRepeating = false, HasRepeated = false };
			}

			var state = keyStates[key];

			if (!state.IsRepeating)
			{
				// Initial press
				state.IsRepeating = true;
				return true; // Trigger immediately on first press
			}

			// Check for repeat
			float delay = state.HasRepeated ? repeatKeyInterval : initialKeyDelay;
			if (Time.time - state.HeldTime >= delay)
			{
				state.HeldTime = Time.time;
				state.HasRepeated = true;
				return true; // Trigger repeat
			}

			return false;
		}
	}
}