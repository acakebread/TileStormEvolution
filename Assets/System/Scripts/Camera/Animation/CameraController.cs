using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		private CameraBase cameraSystem = null;
		private CameraMode currentMode = CameraMode.Absent;
		private Dictionary<CameraState, CameraMode> stateMode = new();
		private Dictionary<CameraMode, CameraState> modeState = new();
		private Dictionary<string, CameraMode[]> groups = new();
		private Dictionary<string, CameraMode> groupMode = new();
		private bool hasCustomStates = false;

		public bool HasCompleted => cameraSystem != null && cameraSystem.HasCompleted;

		private void Awake()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			modeState[CameraMode.Editor] = editorState();
			modeState[CameraMode.Editor].ApplyToCamera(GetComponent<Camera>());
			SetCameraMode(CameraMode.Editor);
		}

		public virtual void Initialise(CameraMode initialMode = CameraMode.Editor)
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			stateMode = new Dictionary<CameraState, CameraMode>();
			modeState = new Dictionary<CameraMode, CameraState>();
			groups = new Dictionary<string, CameraMode[]>();
			groupMode = new Dictionary<string, CameraMode>();

			// Setup camera states before applying the initial mode
			SetupCameraStates();

			// Apply state for initial mode
			var state = GetStateForMode(initialMode);
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
			if (initialMode != CameraMode.Editor || hasCustomStates) SetCameraMode(initialMode);
		}

		//protected void RegisterState(CameraState state, CameraMode[] modes)
		//{
		//	if (state == null || state.data == null)
		//	{
		//		Debug.LogError("Cannot register null CameraState or CameraData");
		//		return;
		//	}
		//	foreach (var mode in modes) modeState[mode] = state;
		//	if (modes.Length > 0) stateMode[state] = modes[0];

		//	hasCustomStates = true;
		//}

		protected void RegisterCamera(CameraState state, CameraMode mode)
		{
			if (state == null || state.data == null)
			{
				Debug.LogError("Cannot register null CameraState or CameraData");
				return;
			}
			modeState[mode] = state;//default - last one registered for now - maybe add an argument later but leave like this for now
			stateMode[state] = mode;
			hasCustomStates = true;
		}

		protected void RegisterGroup(string ID, CameraMode[] modes) => groups[ID] = modes.ToArray();

		private CameraState GetStateForMode(CameraMode mode) => modeState.TryGetValue(mode, out var state) ? state : null;

		private CameraMode GetModeForState(CameraState state) => stateMode.TryGetValue(state, out var mode) ? mode : CameraMode.Absent;

		private bool AreModesSameGroup(CameraMode mode1, CameraMode mode2) 
		{
			foreach (var group in groups)
			{
				if (group.Value.Contains(mode1) && group.Value.Contains(mode2)) 
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

		//public CameraMode GetCurrentModeForMode(CameraMode mode)
		//{
		//	var state = GetStateForMode(mode);
		//	if (state != null) return GetModeForState(state);
		//	return CameraMode.Absent;
		//}

		public CameraMode GetCurrentGroupMode(CameraMode mode)
		{
			var key = ModeGroupKey(mode);
			return null != key && groupMode.ContainsKey(key) ? groupMode[key] : mode;
		}

		public void SetCameraMode(CameraMode mode, bool background = false)
		{
			var state = GetStateForMode(mode);
			if (state == null)
			{
				Debug.LogWarning($"No state registered for CameraMode {mode}. Falling back to Editor mode.");
				return;
			}

			Debug.Log("ModeGroupKey(mode) " + ModeGroupKey(mode));

			var key = ModeGroupKey(mode);
			var group_mode = null != key && groupMode.ContainsKey(key) ? groupMode[key] : mode;
			if (null != key) groupMode[key] = mode;
			stateMode[state] = mode;

			if (background)
			{
				//var currentState = GetStateForMode(currentMode);
				//if (currentState != null && mode != GetModeForState(currentState))
				//{
				//	return;
				//}
				//if (currentState == null)
				//{
				//	Debug.LogWarning($"No state for currentMode {currentMode}");
				//}
				if (false == AreModesSameGroup(mode, currentMode))
					return;
			}

			//var group_mode = GetCurrentModeForMode(mode);
			var group_state = GetStateForMode(group_mode);
			if (null != group_state)
				state.data = group_state.data;

			cameraSystem = mode switch
			{
				CameraMode.Editor => new CameraEditor(state),
				CameraMode.Static => new CameraStatic(state),
				CameraMode.Preset => new CameraPreset(state),
				CameraMode.Follow => new CameraFollow(state),
				CameraMode.Direct => new CameraDirect(state),
				//CameraMode.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(state), _ => new CameraPath(state) },
				CameraMode.Orbit => new CameraOrbit(state),
				CameraMode.Path => new CameraPath(state),
				_ => cameraSystem
			};

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

			//RegisterState(editorState(), new[] { CameraMode.Editor });
			RegisterCamera(editorState(), CameraMode.Editor );
		}

		private CameraState editorState() { return new CameraState { data = new CameraData(GetComponent<Camera>()) { origin = new Vector3(0f, 14f, -14f), target = Vector3.zero }, }; }
		protected virtual (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions() => (new Vector3(0f, 14f, -14f), Vector3.zero);
		protected virtual Func<Vector3> GetTargetPosition() => () => Vector3.zero;
		protected virtual Func<IReadOnlyList<Vector3>> GetFocusPoints() => () => Array.Empty<Vector3>();
	}
}