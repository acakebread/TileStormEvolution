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
		protected Dictionary<CameraMode, CameraBase> cameraSystems = new();
		private Dictionary<string, CameraMode[]> groups = new();
		private Dictionary<string, CameraMode> groupMode = new();

		private bool hasCustomCameras = false;

		public bool HasCompleted => cameraSystem != null && cameraSystem.HasCompleted;

		private void Awake()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			var state = editorState();
			state.ApplyToCamera(GetComponent<Camera>());
			cameraSystems[CameraMode.Editor] = new CameraEditor(state);
			SetCameraMode(CameraMode.Editor);
		}

		public virtual void Initialise(CameraMode initialMode = CameraMode.Editor)
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			groups = new();
			groupMode = new();
			cameraSystems = new();

			// Setup camera states before applying the initial mode
			SetupCameraStates();

			// Apply state for initial mode
			var state = cameraSystems[initialMode].State;
			if (null == state)
			{
				Debug.LogWarning($"No state for mode {initialMode}. Using default Editor position.");
				var (srcPos, dstPos) = GetInitialCameraPositions();
				GetComponent<Camera>().transform.position = srcPos;
				GetComponent<Camera>().transform.rotation = Quaternion.LookRotation(dstPos - srcPos, Vector3.up);
			}
			else
				state.ApplyToCamera(GetComponent<Camera>());

			// Skip SetCameraMode if using default Editor mode and no custom states
			if (initialMode != CameraMode.Editor || hasCustomCameras) SetCameraMode(initialMode);
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

		protected void RegisterGroup(string ID, CameraMode[] modes) => groups[ID] = modes.ToArray();

		private bool AreModesSameGroup(CameraMode m1, CameraMode m2) 
		{
			foreach (var group in groups)
			{
				if (group.Value.Contains(m1) && group.Value.Contains(m2)) 
					return true;
			}
			return false;
		}

		private string ModeGroupKey(CameraMode mode)
		{
			foreach (var group in groups)
			{
				if (group.Value.Contains(mode))
					return group.Key;
			}
			return null;
		}

		public CameraMode GetCurrentGroupMode(CameraMode mode)
		{
			var key = ModeGroupKey(mode);
			return null != key && groupMode.ContainsKey(key) ? groupMode[key] : mode;
		}

		public void SetCameraMode(CameraMode mode, bool background = false)
		{
			var key = ModeGroupKey(mode);
			var group_mode = null != key && groupMode.ContainsKey(key) ? groupMode[key] : mode;
			if (null != key) groupMode[key] = mode;

			if (background && false == AreModesSameGroup(mode, currentMode))
				return;

			var group_state = cameraSystems[group_mode].State;

			cameraSystem = cameraSystems[mode];
			cameraSystem.data = group_state.data;

			currentMode = mode;
			cameraSystem?.Start();
		}

		private void Update() => cameraSystem?.Update();

		private void OnApplicationFocus(bool hasFocus) => cameraSystem?.OnApplicationFocus(hasFocus);

		protected virtual void SetupCameraStates()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("Cannot setup camera states: Camera is null");
				return;
			}

			RegisterCamera(new CameraEditor(editorState()), CameraMode.Editor);
		}

		private CameraState editorState() { return new CameraState { data = new CameraData(GetComponent<Camera>()) { origin = new Vector3(0f, 14f, -14f), target = Vector3.zero }, }; }
		protected virtual (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions() => (new Vector3(0f, 14f, -14f), Vector3.zero);
		protected virtual Func<Vector3> GetTargetPosition() => () => Vector3.zero;
		protected virtual Func<IReadOnlyList<Vector3>> GetFocusPoints() => () => Array.Empty<Vector3>();
	}
}