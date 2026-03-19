using System;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ReadyCallbackRegistry
	{
		private static readonly Dictionary<Type, object> _currentInstances = new(); // new!
		private static readonly Dictionary<Type, List<WeakReference>> _waitList = new();

		public static void RegisterFor<T>(IReadyHandler handler) where T : class
		{
			if (handler == null) return;

			var type = typeof(T);

			// Immediate call if instance already exists
			if (_currentInstances.TryGetValue(type, out var existing) && existing is T instance)
			{
				handler.OnReady(instance);
				return; // optional: return early if you don't want persistent
			}

			// Subscribe for future Raise
			if (!_waitList.TryGetValue(type, out var list))
			{
				list = new List<WeakReference>();
				_waitList[type] = list;
			}

			list.Add(new WeakReference(handler));
		}

		public static void Raise<T>(T instance) where T : class
		{
			if (instance == null) return;

			var type = typeof(T);

			// Remember current instance for late joiners
			_currentInstances[type] = instance;

			if (!_waitList.TryGetValue(type, out var list) || list.Count == 0)
				return;

			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (list[i].Target is IReadyHandler handler)
				{
					handler.OnReady(instance);
				}
				else
				{
					list.RemoveAt(i);
				}
			}

			// Do NOT remove the list anymore — keep for late registrations
			// _waitList.Remove(type); ← DELETE THIS LINE
		}

		public static void Clear<T>() where T : class
		{
			_waitList.Remove(typeof(T));
			_currentInstances.Remove(typeof(T));
		}
	}

	public interface IReadyHandler
	{
		void OnReady(object target);
	}
}