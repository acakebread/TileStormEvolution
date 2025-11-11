using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
	[ExecuteAlways]
	public class ParticleDebugger : MonoBehaviour
	{
		[Header("Display")]
		public bool showInPlayMode = true;
		public bool showInEditMode = true;
		public Vector2 screenOffset = new Vector2(15, 15);
		public int maxWidth = 200;
		public int maxHeight = 100;

		[Header("Update")]
		[Range(0.05f, 1f)] public float guiUpdateInterval = 0.1f;

		private List<ParticleController> _controllers = new();
		private float _lastGuiUpdate = 0f;
		private int _totalSlots = 0;
		private int _totalActive = 0;

		private void OnEnable()
		{
			RefreshControllers();
		}

		private void Update()
		{
			if (Application.isPlaying)
				RefreshControllers();
		}

		private void RefreshControllers()
		{
			var all = FindObjectsByType<ParticleController>(FindObjectsSortMode.None);
			if (_controllers.Count != all.Length || !_controllers.SequenceEqual(all))
				_controllers = all.ToList();
		}

		private void OnGUI()
		{
			bool shouldDraw = (Application.isPlaying && showInPlayMode) ||
							  (!Application.isPlaying && showInEditMode);
			if (!shouldDraw || _controllers.Count == 0) return;

			float now = Time.unscaledTime;
			if (now - _lastGuiUpdate >= guiUpdateInterval)
			{
				_totalSlots = _controllers.Sum(c => c.customParticleSystem?.ViewCount ?? 0);
				_totalActive = _controllers.Sum(c => c.customParticleSystem?.ActiveParticleCount ?? 0);
				_lastGuiUpdate = now;
			}

			int totalCapacity = ParticleSystem.MaxViewCache * _controllers.Count;

			GUI.color = Color.cyan;
			GUI.skin.label.fontStyle = FontStyle.Bold;
			GUI.skin.label.fontSize = 12;

			GUILayout.BeginArea(new Rect(screenOffset.x, screenOffset.y, maxWidth, maxHeight), GUI.skin.box);
			GUILayout.Label("<color=white><b>PARTICLE SYSTEM DEBUG</b></color>");

			GUILayout.Label(
				$"<color=yellow>ParticleMesh slots used:</color> " +
				$"<color=white>{_totalSlots}</color>/<color=lime>{totalCapacity}</color>");

			GUILayout.Label(
				$"<color=yellow>Active particles:</color> <color=white>{_totalActive}</color>");

			GUILayout.EndArea();
		}

#if UNITY_EDITOR
		private void Reset() => OnEnable();
		private void OnValidate() => RefreshControllers();
#endif
	}
#endif
}