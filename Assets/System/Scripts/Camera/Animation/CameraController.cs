using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		private CameraMode currentMode = CameraMode.Absent;
		private CameraBase cameraSystem = null;
		private Dictionary<CameraMode, CameraBase> cameraSystems = new();
		private Dictionary<string, CameraMode[]> groups = new();
		private Dictionary<string, CameraMode> groupModes = new();

		private bool hasCustomCameras = false;

		protected Dictionary<CameraMode, CameraBase> CameraSystems { get => cameraSystems; }
		public bool HasCompleted => cameraSystem is CameraOrbit orbit ? orbit.HasCompleted : cameraSystem is CameraPath path && path.HasCompleted;

		private void Awake()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			cameraSystems[CameraMode.Default] = new CameraDefault(defaultConfig());
			cameraSystems[CameraMode.Default].Awake();
			SetCameraMode(CameraMode.Default);
		}

		public virtual void Initialise(CameraMode initialMode = CameraMode.Default)
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			groups = new();
			groupModes = new();
			cameraSystems = new();

			// Setup camera configs before applying the initial mode
			SetupCameras();

			// Apply config for initial mode
			if (cameraSystems.ContainsKey(initialMode))
				cameraSystems[initialMode].Awake();
			else
			{
				Debug.LogWarning($"No config for mode {initialMode}. Using default position.");
				var (srcPos, dstPos) = GetInitialCameraPositions();
				GetComponent<Camera>().transform.position = srcPos;
				GetComponent<Camera>().transform.rotation = Quaternion.LookRotation(dstPos - srcPos, Vector3.up);
			}

			// Skip SetCameraMode if using default mode and no custom configs
			if (initialMode != CameraMode.Default || hasCustomCameras) SetCameraMode(initialMode);
		}

		protected void RegisterCamera(CameraBase camera, CameraMode mode)
		{
			if (null == camera)
			{
				Debug.LogError("Cannot register null Camera");
				return;
			}
			cameraSystems[mode] = camera;
			hasCustomCameras = true;
		}

		protected void RegisterGroup(string groupId, CameraMode[] modes)
		{
			if (string.IsNullOrEmpty(groupId) || modes == null || modes.Length == 0)
			{
				Debug.LogWarning("Invalid group registration.");
				return;
			}
			groups[groupId] = modes.ToArray();
		}

		private string GetGroupIdForMode(CameraMode mode) => groups.FirstOrDefault(group => group.Value.Contains(mode)).Key;

		public CameraMode GetCurrentGroupMode(CameraMode mode)
		{
			var key = GetGroupIdForMode(mode);
			return null != key && groupModes.ContainsKey(key) ? groupModes[key] : mode;
		}

		public void SetCameraMode(CameraMode mode, bool background = false)
		{
			var key = GetGroupIdForMode(mode);
			var groupMode = null != key && groupModes.ContainsKey(key) ? groupModes[key] : mode;
			if (null != key) groupModes[key] = mode;

			if (background && false == AreModesInSameGroup(mode, currentMode))
				return;

			cameraSystem = cameraSystems[mode];
			if (null == cameraSystem)
				return;

			currentMode = mode;
			cameraSystem.CopyFrom(cameraSystems[groupMode]); 
			cameraSystem.Start();

			//local function
			bool AreModesInSameGroup(CameraMode mode1, CameraMode mode2) => groups.Any(group => group.Value.Contains(mode1) && group.Value.Contains(mode2));
		}

		private void Update() => cameraSystem?.Update();

		private void OnApplicationFocus(bool hasFocus) => cameraSystem?.OnApplicationFocus(hasFocus);

		protected virtual void SetupCameras()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("Cannot setup camera configs: Camera is null");
				return;
			}

			RegisterCamera(new CameraDefault(defaultConfig()), CameraMode.Default);
		}

		private CameraConfig defaultConfig() { return new CameraConfig { data = new CameraData(GetComponent<Camera>()) { iorigin = new Vector3(0f, 14f, -14f), itarget = Vector3.zero }, }; }
		protected virtual (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions() => (new Vector3(0f, 14f, -14f), Vector3.zero);
		protected virtual Func<Vector3> GetTargetPosition() => () => Vector3.zero;
		protected virtual Func<IReadOnlyList<Vector3>> GetFocusPoints() => () => Array.Empty<Vector3>();
	}
}