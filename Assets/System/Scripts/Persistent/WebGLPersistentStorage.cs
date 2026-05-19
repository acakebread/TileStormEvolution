using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class WebGLPersistentStorage
	{
#if UNITY_WEBGL && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern void MassiveHadron_SyncPersistentData(int populate, string receiverObject, string onCompleteMethod);
#endif

		private static SyncHost host;
		private static bool initialLoadCompleted;
		private static bool initialLoadInProgress;
		private static bool flushInProgress;
		private static bool flushQueued;
		private static readonly List<Action<bool>> InitialLoadCallbacks = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void ResetState()
		{
			host = null;
			initialLoadCompleted = false;
			initialLoadInProgress = false;
			flushInProgress = false;
			flushQueued = false;
			InitialLoadCallbacks.Clear();
		}

		public static void EnsureLoaded(Action<bool> onComplete = null)
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			if (initialLoadCompleted)
			{
				onComplete?.Invoke(true);
				return;
			}

			if (onComplete != null)
				InitialLoadCallbacks.Add(onComplete);

			if (initialLoadInProgress)
				return;

			initialLoadInProgress = true;
			EnsureHost().StartInitialLoadSync();
#else
			onComplete?.Invoke(true);
#endif
		}

		public static void Flush()
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			if (initialLoadInProgress)
			{
				flushQueued = true;
				return;
			}

			if (flushInProgress)
			{
				flushQueued = true;
				return;
			}

			flushInProgress = true;
			EnsureHost().StartFlushSync();
#endif
		}

		private static SyncHost EnsureHost()
		{
			if (host != null)
				return host;

			var go = new GameObject(nameof(WebGLPersistentStorage));
			go.hideFlags = HideFlags.HideAndDontSave;
			UnityEngine.Object.DontDestroyOnLoad(go);
			host = go.AddComponent<SyncHost>();
			return host;
		}

		private static void CompleteInitialLoad(bool success)
		{
			initialLoadInProgress = false;
			initialLoadCompleted = success;

			var callbacks = InitialLoadCallbacks.ToArray();
			InitialLoadCallbacks.Clear();
			foreach (var callback in callbacks)
				callback?.Invoke(success);

			if (flushQueued)
			{
				flushQueued = false;
				Flush();
			}
		}

		private static void CompleteFlush(bool success)
		{
			flushInProgress = false;

			if (!success)
				Debug.LogWarning("WebGLPersistentStorage: persistent data flush failed.");

			if (flushQueued)
			{
				flushQueued = false;
				Flush();
			}
		}

		private sealed class SyncHost : MonoBehaviour
		{
			public void StartInitialLoadSync()
			{
#if UNITY_WEBGL && !UNITY_EDITOR
				MassiveHadron_SyncPersistentData(1, gameObject.name, nameof(HandleInitialLoadComplete));
#endif
			}

			public void StartFlushSync()
			{
#if UNITY_WEBGL && !UNITY_EDITOR
				MassiveHadron_SyncPersistentData(0, gameObject.name, nameof(HandleFlushComplete));
#endif
			}

			public void HandleInitialLoadComplete(string payload)
			{
				var success = ParseSuccess(payload, out var message);
				if (!success)
					Debug.LogWarning($"WebGLPersistentStorage: initial persistent data sync failed: {message}");

				CompleteInitialLoad(success);
			}

			public void HandleFlushComplete(string payload)
			{
				var success = ParseSuccess(payload, out var message);
				if (!success)
					Debug.LogWarning($"WebGLPersistentStorage: persistent data flush failed: {message}");

				CompleteFlush(success);
			}

			private static bool ParseSuccess(string payload, out string message)
			{
				message = null;
				if (string.IsNullOrWhiteSpace(payload))
					return false;

				var separator = payload.IndexOf('|');
				if (separator < 0)
					return string.Equals(payload.Trim(), "1", StringComparison.Ordinal);

				var result = payload.Substring(0, separator).Trim();
				message = payload.Substring(separator + 1).Trim();
				return string.Equals(result, "1", StringComparison.Ordinal);
			}
		}
	}
}
