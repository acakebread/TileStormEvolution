using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	public class ParticleDebugger : MonoBehaviour
	{
		[Header("Display")]
		public bool showInPlayMode = true;
		public bool showInEditMode = true; // Kept, but will be ignored in builds
		public Vector2 screenOffset = new Vector2(15, 15);

		[Header("Update")]
		[Range(0.05f, 1f)] public float guiUpdateInterval = 0.1f;

		private List<ParticleController> _controllers = new();
		private float _lastGuiUpdate = 0f;
		private int _totalSlots = 0;
		private int _totalActive = 0;

		// Global toggle state
		private bool _globalUpdateParticles = true;
		private bool _applyPending = false;

		private void OnEnable()
		{
			RefreshControllers();
		}

		private void Update()
		{
			if (Application.isPlaying)
				RefreshControllers();

			// Apply toggle change to all controllers
			if (_applyPending && _controllers.Count > 0)
			{
				foreach (var controller in _controllers)
				{
					if (controller != null)
						controller.updateParticles = _globalUpdateParticles;
				}
				_applyPending = false;
			}
		}

		private void RefreshControllers()
		{
			var all = FindObjectsByType<ParticleController>(FindObjectsSortMode.None);
			bool changed = _controllers.Count != all.Length || !_controllers.SequenceEqual(all);

			if (changed)
			{
				_controllers = all.ToList();

				// Sync toggle to current state (use first if mixed)
				if (_controllers.Count > 0)
				{
					_globalUpdateParticles = _controllers[0].updateParticles;
					_applyPending = true; // force sync on change
				}
			}
		}

		private void OnGUI()
		{
			// Only draw in Play Mode (even in Editor)
			if (!Application.isPlaying || !showInPlayMode || _controllers.Count == 0) return;

			// Update stats
			float now = Time.unscaledTime;
			if (now - _lastGuiUpdate >= guiUpdateInterval)
			{
				_totalSlots = _controllers.Sum(c => c.customParticleSystem?.ViewCount ?? 0);
				_totalActive = _controllers.Sum(c => c.customParticleSystem?.ActiveParticleCount ?? 0);
				_lastGuiUpdate = now;
			}

			int totalCapacity = ParticleSystem.MaxViewCache * _controllers.Count;

			// GUI Style
			GUI.color = Color.cyan;
			GUI.skin.label.fontStyle = FontStyle.Bold;
			GUI.skin.label.fontSize = 12;
			GUI.skin.toggle.fontSize = 11;
			GUI.skin.toggle.normal.textColor = Color.white;

			// YOUR EXACT SIZE
			float width = 200;
			float height = 100;
			var areaRect = new Rect(screenOffset.x, screenOffset.y, width, height);

			GUILayout.BeginArea(areaRect, GUI.skin.box);

			GUILayout.Label("<color=white><b>PARTICLE SYSTEM DEBUG</b></color>");

			GUILayout.Label(
				$"<color=yellow>Slots:</color> <color=white>{_totalSlots}</color>/<color=lime>{totalCapacity}</color>");

			GUILayout.Label(
				$"<color=yellow>Active:</color> <color=white>{_totalActive}</color>");

			// Toggle
			bool newUpdate = GUILayout.Toggle(_globalUpdateParticles, " Update Particles");
			if (newUpdate != _globalUpdateParticles)
			{
				_globalUpdateParticles = newUpdate;
				_applyPending = true;
			}

			GUILayout.EndArea();
		}

#if UNITY_EDITOR
		private void Reset() => OnEnable();
		private void OnValidate() => RefreshControllers();
#endif
	}
}