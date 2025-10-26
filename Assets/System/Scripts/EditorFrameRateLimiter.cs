using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Limits the editor frame rate to prevent excessive CPU/GPU use when testing.
/// Attach to any GameObject and enable/disable as needed.
/// </summary>
[ExecuteAlways]
public class EditorFrameRateLimiter : MonoBehaviour
{
	[Tooltip("Target frame rate when running in the Editor.")]
	public int targetFrameRate = 60;

	[Tooltip("Whether to apply the frame rate limit in the Editor.")]
	public bool enableLimit = true;

	private int previousFrameRate;

	private void OnEnable()
	{
#if UNITY_EDITOR
		previousFrameRate = Application.targetFrameRate;
		ApplyLimit();
		EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
	}

	private void OnDisable()
	{
#if UNITY_EDITOR
		RestorePreviousLimit();
		EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
	}

#if UNITY_EDITOR
	private void OnPlayModeChanged(PlayModeStateChange state)
	{
		// Re-apply the frame rate when entering/exiting play mode
		if (state == PlayModeStateChange.EnteredPlayMode ||
			state == PlayModeStateChange.EnteredEditMode)
		{
			ApplyLimit();
		}
	}
#endif

	private void Update()
	{
#if UNITY_EDITOR
		if (enableLimit)
			ApplyLimit();
		else
			RestorePreviousLimit();
#endif
	}

	private void ApplyLimit()
	{
		if (enableLimit)
		{
			Application.targetFrameRate = targetFrameRate;
			QualitySettings.vSyncCount = 0; // Disable VSync so frame rate cap applies
		}
	}

	private void RestorePreviousLimit()
	{
		Application.targetFrameRate = previousFrameRate;
	}
}
