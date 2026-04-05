using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	public class CameraController : MonoBehaviour
	{
		public CameraBase activeSystem => cameraSystems.ContainsKey(currentSystem) ? cameraSystems[currentSystem] : null;
		protected Dictionary<string, CameraBase> CameraSystems => cameraSystems;

		private const string DefaultSystem = "Default"; // Define default mode in core library
		private string currentSystem = DefaultSystem;

		private Dictionary<string, CameraBase> cameraSystems = new();
		private Dictionary<string, string[]> modes = new();
		private Dictionary<string, string> modeSystems = new();
		private HashSet<string> startedSystems = new();

		protected virtual void Awake()
		{
			var camera = null != Camera.main ? Camera.main : FindAnyObjectByType<Camera>();//any camera
			if (null == camera)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			var (srcPos, dstPos) = GetInitialCameraPositions();
			RegisterCamera(new CameraDefault(camera) { iorigin = srcPos, itarget = dstPos }, DefaultSystem);
			SetCameraSystem(DefaultSystem);
		}

		protected void Initialise(string initialSystem = null)
		{
			modes = new();
			modeSystems = new();
			cameraSystems = new();
			SetupCameras();
			SetCameraSystem(string.IsNullOrEmpty(initialSystem) ? DefaultSystem : initialSystem);
		}

		public void SetCameraMode(string mode)
		{
			if (false == modeSystems.ContainsKey(mode)) return;
			var system = GetCurrentModeSystem(modeSystems[mode]);
			SetCameraSystem(system);

			string GetCurrentModeSystem(string system)
			{
				if (string.IsNullOrEmpty(system))
				{
					Debug.LogWarning($"Invalid system: {system}. Returning default system.");
					return DefaultSystem;
				}
				var key = GetModeIDForSystem(system);
				return key != null && modeSystems.ContainsKey(key) ? modeSystems[key] : system;
			}
		}

		public void SetCameraSystem(string system, bool background = false)
		{
			//if (currentSystem == system) return;//there may be an optimisation here but currently we still need to copy some properties even if the system is the same so disable for now
			if (!cameraSystems.ContainsKey(system))
			{
				Debug.LogWarning($"Invalid camera mode: {system}. Falling back to '{DefaultSystem}'.");
				system = DefaultSystem;
			}

			var key = GetModeIDForSystem(system);
			var modeSystem = key != null && modeSystems.ContainsKey(key) ? modeSystems[key] : system;
			if (key != null) modeSystems[key] = system;

			if (background && !AreSystemsInSameMode(system, currentSystem))
				return;

			if (!cameraSystems.ContainsKey(system))
			{
				Debug.LogWarning($"No camera system registered for mode '{system}'.");
				return;
			}

			activeSystem?.OnDisable();
			currentSystem = system;
			activeSystem?.CopyFrom(cameraSystems.ContainsKey(modeSystem) ? cameraSystems[modeSystem] : null);
			if (!startedSystems.Contains(system))
			{
				activeSystem?.Start();
				startedSystems.Add(system);
			}
			activeSystem?.OnEnable();

			bool AreSystemsInSameMode(string system1, string system2) => modes.Any(mode => mode.Value.Contains(system1) && mode.Value.Contains(system2));
		}

		protected virtual (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions() => (new Vector3(0f, 0f, -10f), Vector3.forward);

		protected virtual void SetupCameras() { }

		protected void RegisterCamera(CameraBase camera, string system)
		{
			if (string.IsNullOrEmpty(system))
			{
				Debug.LogError("Cannot register camera with null or empty mode");
				return;
			}

			if (cameraSystems.ContainsKey(system))
				Debug.LogWarning($"Camera mode '{system}' is already registered. Overwriting.");

			cameraSystems[system] = camera;
			cameraSystems[system].Awake();
		}

		protected void RegisterMode(string modeId, string[] systems)
		{
			if (string.IsNullOrEmpty(modeId) || systems == null || systems.Length == 0 || systems.Any(string.IsNullOrEmpty))
			{
				Debug.LogWarning("Invalid mode registration: Mode ID or systems are null/empty.");
				return;
			}
			if (modes.ContainsKey(modeId))
				Debug.LogWarning($"Mode '{modeId}' is already registered. Overwriting.");

			modes[modeId] = systems.ToArray();
		}

		private string GetModeIDForSystem(string system) => modes.FirstOrDefault(mode => mode.Value.Contains(system)).Key;

		protected virtual void OnEnable() => activeSystem?.OnEnable();
		
		protected virtual void Update() => activeSystem?.Update();

		protected virtual void OnGUI() => activeSystem?.OnGUI();

		protected virtual void OnDisable() => activeSystem?.OnDisable();

		protected virtual void OnDestroy() => activeSystem?.OnDestroy();

		protected virtual void OnApplicationFocus(bool hasFocus) => activeSystem?.OnApplicationFocus(hasFocus);

		public void AdjustAllCamerasForMapShift(Vector3 delta)
		{
			if (delta == Vector3.zero) return;

			foreach (var sys in cameraSystems.Values)
			{
				sys.OnMapOriginShift(delta);
			}
		}
	}
}