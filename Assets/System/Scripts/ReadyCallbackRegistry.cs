using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Centralized registry for "ready" callbacks.
	/// Allows components to register interest in when a service/component of type T becomes available.
	/// Supports immediate invocation if already ready, deferred invocation otherwise,
	/// and optional auto-cleanup when the registering object is destroyed.
	/// </summary>
	public static class ReadyCallbackRegistry
	{
		// Type → multicast Action<T>
		private static readonly Dictionary<Type, Delegate> _callbacks = new();

		/// <summary>
		/// Register interest in when T becomes ready.
		/// </summary>
		/// <typeparam name="T">The type of object we're waiting for (class or interface)</typeparam>
		/// <param name="onReady">Callback to invoke when T is ready (or immediately if already ready)</param>
		/// <param name="getCurrent">Optional function that returns current instance if already available</param>
		/// <param name="lifetimeOwner">Optional Unity object — auto-unregisters when this is destroyed</param>
		/// <param name="persistent">If true, callbacks remain after Raise (default: false = fire-once)</param>
		public static void Register<T>(
			Action<T> onReady,
			Func<T>? getCurrent = null,
			UnityEngine.Object? lifetimeOwner = null,
			bool persistent = false)
			where T : class
		{
			if (onReady == null) return;

			var type = typeof(T);

			// Immediate case: already exists?
			if (getCurrent != null)
			{
				T? current = getCurrent.Invoke();
				if (current != null)
				{
					try
					{
						onReady.Invoke(current);
						if (!persistent) return; // early exit — no need to subscribe
					}
					catch (Exception ex)
					{
						Debug.LogException(ex);
					}
				}
			}

			// Subscribe for future readiness
			if (!_callbacks.TryGetValue(type, out var existing))
			{
				_callbacks[type] = onReady;
			}
			else
			{
				_callbacks[type] = Delegate.Combine(existing, onReady);
			}

			// Auto-cleanup hook if lifetime owner provided
			if (lifetimeOwner != null && lifetimeOwner is not GameObject go)
			{
				// If it's a component → use its GameObject
				if (lifetimeOwner is Component comp && comp.gameObject != null)
				{
					lifetimeOwner = comp.gameObject;
				}
			}

			if (lifetimeOwner is GameObject gameObject && gameObject != null)
			{
				var hook = gameObject.GetComponent<ReadyCleanupHook>();
				if (hook == null)
				{
					hook = gameObject.AddComponent<ReadyCleanupHook>();
				}
				hook.Track(typeof(T), onReady);
			}
		}

		/// <summary>
		/// Remove a specific callback registration
		/// </summary>
		public static void Unregister<T>(Action<T> onReady)
			where T : class
		{
			var type = typeof(T);
			if (!_callbacks.TryGetValue(type, out var del)) return;

			var newDel = Delegate.Remove(del, onReady);
			if (newDel == null)
				_callbacks.Remove(type);
			else
				_callbacks[type] = newDel;
		}

		/// <summary>
		/// Notify all registered listeners that T is now ready
		/// </summary>
		/// <param name="instance">The newly ready instance</param>
		/// <param name="persistent">If true, keeps callbacks alive after this call</param>
		public static void Raise<T>(T instance, bool persistent = false)
			where T : class
		{
			if (instance == null) return;

			var type = typeof(T);
			if (!_callbacks.TryGetValue(type, out var del)) return;

			try
			{
				if (del is Action<T> action)
				{
					action.Invoke(instance);
				}
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}

			if (!persistent)
			{
				_callbacks.Remove(type);
			}
		}

		/// <summary>
		/// Remove all callbacks for a given type
		/// </summary>
		public static void Clear<T>() where T : class
		{
			_callbacks.Remove(typeof(T));
		}

		/// <summary>
		/// Clear all registrations (useful on quit / domain reload)
		/// </summary>
		public static void ClearAll()
		{
			_callbacks.Clear();
		}

		/// <summary>
		/// Debugging helper: how many listeners for this type?
		/// </summary>
		public static int GetCount<T>() where T : class
		{
			if (_callbacks.TryGetValue(typeof(T), out var del))
				return del?.GetInvocationList().Length ?? 0;
			return 0;
		}

		// ────────────────────────────────────────────────────────────────
		//  Tiny internal helper for auto-unregistration
		// ────────────────────────────────────────────────────────────────

		private class ReadyCleanupHook : MonoBehaviour
		{
			private readonly Dictionary<Type, List<Delegate>> _tracked = new();

			public void Track(Type type, Delegate callback)
			{
				if (!_tracked.TryGetValue(type, out var list))
				{
					list = new List<Delegate>();
					_tracked[type] = list;
				}
				list.Add(callback);
			}

			private void OnDestroy()
			{
				foreach (var kv in _tracked)
				{
					var type = kv.Key;
					foreach (var del in kv.Value)
					{
						if (_callbacks.TryGetValue(type, out var current))
						{
							var removed = Delegate.Remove(current, del);
							if (removed == null)
								_callbacks.Remove(type);
							else
								_callbacks[type] = removed;
						}
					}
				}
				_tracked.Clear();
			}
		}
	}
}