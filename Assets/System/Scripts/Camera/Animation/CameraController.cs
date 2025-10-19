using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		public CameraBase currentSystem => cameraSystems.ContainsKey(currentMode) ? cameraSystems[currentMode] : null;

		private const string DefaultMode = "Default"; // Define default mode in core library
		private string currentMode = DefaultMode;

		private Dictionary<string, CameraBase> cameraSystems = new();
		private Dictionary<string, string[]> groups = new();
		private Dictionary<string, string> groupModes = new();
		private bool hasCustomCameras = false;

		protected Dictionary<string, CameraBase> CameraSystems => cameraSystems;

		private void Awake()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			RegisterCamera(new CameraDefault(GetComponent<Camera>()), DefaultMode);
			SetCameraMode(DefaultMode);
		}

		public virtual void Initialise(string initialMode = null)
		{
			initialMode = string.IsNullOrEmpty(initialMode) ? DefaultMode : initialMode;

			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			groups = new();
			groupModes = new();
			cameraSystems = new();

			SetupCameras();

			if (!cameraSystems.ContainsKey(initialMode))
			{
				Debug.LogWarning($"No config for mode '{initialMode}'. Using default position.");
				var (srcPos, dstPos) = GetInitialCameraPositions();
				GetComponent<Camera>().transform.position = srcPos;
				GetComponent<Camera>().transform.rotation = Quaternion.LookRotation(dstPos - srcPos, Vector3.up);
			}

			if (initialMode != DefaultMode || hasCustomCameras)
			{
				SetCameraMode(initialMode);
			}
		}

		protected void RegisterCamera(CameraBase camera, string mode)
		{
			if (camera == null)
			{
				Debug.LogError("Cannot register null Camera");
				return;
			}
			if (string.IsNullOrEmpty(mode))
			{
				Debug.LogError("Cannot register camera with null or empty mode");
				return;
			}
			if (cameraSystems.ContainsKey(mode))
			{
				Debug.LogWarning($"Camera mode '{mode}' is already registered. Overwriting.");
			}
			cameraSystems[mode] = camera;
			cameraSystems[mode].Awake();
			hasCustomCameras = true;
		}

		protected void RegisterGroup(string groupId, string[] modes)
		{
			if (string.IsNullOrEmpty(groupId) || modes == null || modes.Length == 0 || modes.Any(string.IsNullOrEmpty))
			{
				Debug.LogWarning("Invalid group registration: Group ID or modes are null/empty.");
				return;
			}
			if (groups.ContainsKey(groupId))
			{
				Debug.LogWarning($"Group '{groupId}' is already registered. Overwriting.");
			}
			groups[groupId] = modes.ToArray();
		}

		private string GetGroupIdForMode(string mode) => groups.FirstOrDefault(group => group.Value.Contains(mode)).Key;

		public string GetCurrentGroupMode(string mode)
		{
			if (string.IsNullOrEmpty(mode))
			{
				Debug.LogWarning($"Invalid mode: {mode}. Returning default mode.");
				return DefaultMode;
			}
			var key = GetGroupIdForMode(mode);
			return key != null && groupModes.ContainsKey(key) ? groupModes[key] : mode;
		}

		public void SetCameraMode(string mode, bool background = false)
		{
			if (string.IsNullOrEmpty(mode))
			{
				Debug.LogWarning($"Invalid camera mode: {mode}. Falling back to '{DefaultMode}'.");
				mode = DefaultMode;
			}

			var key = GetGroupIdForMode(mode);
			var groupMode = key != null && groupModes.ContainsKey(key) ? groupModes[key] : mode;
			if (key != null) groupModes[key] = mode;

			if (background && !AreModesInSameGroup(mode, currentMode))
				return;

			if (!cameraSystems.ContainsKey(mode))
			{
				Debug.LogWarning($"No camera system registered for mode '{mode}'.");
				return;
			}

			currentMode = mode;
			currentSystem?.CopyFrom(cameraSystems.ContainsKey(groupMode) ? cameraSystems[groupMode] : null);
			currentSystem?.Start();

			bool AreModesInSameGroup(string mode1, string mode2) => groups.Any(group => group.Value.Contains(mode1) && group.Value.Contains(mode2));
		}

		protected virtual void Update() => currentSystem?.Update();

		protected virtual void OnGUI() => currentSystem?.OnGUI();

		protected virtual void OnPostRender() => currentSystem?.OnPostRender();

		protected virtual void OnDestroy() => currentSystem?.OnDestroy();

		private void OnApplicationFocus(bool hasFocus) => currentSystem?.OnApplicationFocus(hasFocus);

		protected virtual void SetupCameras() { }

		protected virtual (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions() => (new Vector3(0f, 0f, 0f), Vector3.forward);
	}
}