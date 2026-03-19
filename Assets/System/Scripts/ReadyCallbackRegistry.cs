using System;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ReadyCallbackRegistry
	{
		private static readonly Dictionary<Type, object> _currentInstances = new();

		// Store List<Action<T>> strongly — simple, reliable
		private static readonly Dictionary<Type, List<Delegate>> _waitList = new();

		public static void RegisterFor<T>(Action<T> onReady) where T : class
		{
			if (onReady == null) return;

			var type = typeof(T);

			// Immediate
			if (_currentInstances.TryGetValue(type, out var existing) && existing is T instance)
			{
				onReady(instance);
				return;
			}

			if (!_waitList.TryGetValue(type, out var list))
			{
				list = new List<Delegate>();
				_waitList[type] = list;
			}

			list.Add(onReady);
		}

		public static void Raise<T>(T instance, bool clearAfter = true) where T : class
		{
			if (instance == null) return;

			var type = typeof(T);
			_currentInstances[type] = instance;

			if (!_waitList.TryGetValue(type, out var list) || list.Count == 0)
				return;

			foreach (var del in list)
			{
				if (del is Action<T> action)
				{
					try
					{
						action(instance);
					}
					catch (Exception ex)
					{
						UnityEngine.Debug.LogException(ex);
					}
				}
			}

			if (clearAfter)
				_waitList.Remove(type);
		}

		public static void Clear<T>() where T : class
		{
			_waitList.Remove(typeof(T));
			_currentInstances.Remove(typeof(T));
		}
	}
}