using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MassiveHadronLtd
{
	public sealed class Ref<T> : IDisposable where T : UnityEngine.Object
	{
		private T _value;
		private int _refCount = 1;

		private static readonly Dictionary<EntityId, Ref<T>> _allRefs = new();

		public T Value => _value;

		public static implicit operator T(Ref<T> r) => r?._value;

		private Ref(T value)
		{
			_value = value ?? throw new ArgumentNullException(nameof(value));
			_allRefs[value.GetEntityId()] = this;
		}

		public static Ref<T> Create(Func<T> factory)
		{
			var obj = factory();
			if (obj == null)
				throw new InvalidOperationException($"Factory returned null for {typeof(T).Name}");
			return new Ref<T>(obj);
		}

		public static Ref<Cubemap> CreateCubemap(int resolution, TextureFormat format, bool mipmap)
		{
			var cubemap = new Cubemap(resolution, format, mipmap);
			return new Ref<Cubemap>(cubemap);
		}

		// Simple and reliable way to assign
		public void Set(Ref<T> newRef)
		{
			// Release old
			if (_value != null)
			{
				_refCount--;
				if (_refCount <= 0)
					DestroyInternal();
			}

			// Take new
			if (newRef != null && newRef._value != null)
			{
				_value = newRef._value;
				_refCount = 1;
				newRef._refCount++;
			}
			else
			{
				_value = null;
				_refCount = 0;
			}
		}

		public void Dispose() => Set(null);

		private void DestroyInternal()
		{
			if (_value == null) return;

			_allRefs.Remove(_value.GetEntityId());

			if (Application.isPlaying)
				UnityEngine.Object.Destroy(_value);
			else
				UnityEngine.Object.DestroyImmediate(_value);

			_value = null;
		}

		public static void ClearAll()
		{
			foreach (var r in _allRefs.Values.ToArray())
				r.DestroyInternal();
			_allRefs.Clear();
		}
	}
}


//using UnityEngine;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace MassiveHadronLtd
//{
//	public sealed class Ref<T> : IDisposable where T : UnityEngine.Object
//	{
//		private T _value;
//		private int _refCount = 1;

//		private static readonly Dictionary<EntityId, Ref<T>> _allRefs = new();

//		public T Value => _value;

//		public static implicit operator T(Ref<T> r) => r?._value;

//		private Ref(T value)
//		{
//			_value = value ?? throw new ArgumentNullException(nameof(value));
//			_allRefs[value.GetEntityId()] = this;
//		}

//		public static Ref<T> Create(Func<T> factory)
//		{
//			var obj = factory();
//			if (obj == null)
//				throw new InvalidOperationException($"Factory returned null for {typeof(T).Name}");
//			return new Ref<T>(obj);
//		}

//		public static Ref<Cubemap> CreateCubemap(int resolution, TextureFormat format, bool mipmap)
//		{
//			var cubemap = new Cubemap(resolution, format, mipmap);
//			return new Ref<Cubemap>(cubemap);
//		}

//		public Ref<T> Assign(Ref<T> newRef)
//		{
//			if (_value != null)
//			{
//				_refCount--;
//				if (_refCount <= 0)
//					DestroyInternal();
//			}

//			if (newRef != null && newRef._value != null)
//			{
//				_value = newRef._value;
//				_refCount = 1;
//				newRef._refCount++;
//			}
//			else
//			{
//				_value = null;
//				_refCount = 0;
//			}
//			return this;
//		}

//		public void Dispose() => Assign(null);

//		private void DestroyInternal()
//		{
//			if (_value == null) return;

//			_allRefs.Remove(_value.GetEntityId());

//			if (Application.isPlaying)
//				UnityEngine.Object.Destroy(_value);
//			else
//				UnityEngine.Object.DestroyImmediate(_value);

//			_value = null;
//		}

//		public static void ClearAll()
//		{
//			foreach (var r in _allRefs.Values.ToArray())
//				r.DestroyInternal();
//			_allRefs.Clear();
//		}
//	}
//}

//using UnityEngine;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace MassiveHadronLtd
//{
//	public sealed class Ref<T> : IDisposable where T : UnityEngine.Object
//	{
//		private T _value;
//		private int _refCount = 1;

//		// Use EntityId as the key (this is the future-proof replacement)
//		private static readonly Dictionary<EntityId, Ref<T>> _allRefs = new();

//		public T Value => _value;

//		// Implicit conversion so Ref<T> can be used like the raw object
//		public static implicit operator T(Ref<T> r) => r?._value;

//		private Ref(T value)
//		{
//			_value = value ?? throw new ArgumentNullException(nameof(value));
//			_allRefs[value.GetEntityId()] = this;
//		}

//		public static Ref<T> Create(Func<T> factory)
//		{
//			var obj = factory();
//			if (obj == null)
//				throw new InvalidOperationException($"Factory returned null for {typeof(T).Name}");

//			return new Ref<T>(obj);
//		}

//		// Specific factory for Cubemap (avoids generic lambda inference issues)
//		public static Ref<Cubemap> CreateCubemap(int resolution, TextureFormat format, bool mipmap)
//		{
//			Cubemap cubemap = new Cubemap(resolution, format, mipmap);
//			return new Ref<Cubemap>(cubemap);
//		}

//		// Add more specific factories when needed, e.g.:
//		// public static Ref<Texture2D> CreateTexture2D(int width, int height, TextureFormat format, bool mipmap = false)
//		//     => new Ref<Texture2D>(new Texture2D(width, height, format, mipmap));

//		// ─────────────────────────────────────────────────────────────────────
//		// Automatic reference counting on reassignment
//		// ─────────────────────────────────────────────────────────────────────
//		public Ref<T> Assign(Ref<T> newRef)
//		{
//			// Release old value
//			if (_value != null)
//			{
//				_refCount--;
//				if (_refCount <= 0)
//					DestroyInternal();
//			}

//			// Acquire new value
//			if (newRef != null && newRef._value != null)
//			{
//				_value = newRef._value;
//				_refCount = 1;
//				newRef._refCount++;
//			}
//			else
//			{
//				_value = null;
//				_refCount = 0;
//			}

//			return this;
//		}

//		public void Dispose() => Assign(null);

//		private void DestroyInternal()
//		{
//			if (_value == null) return;

//			_allRefs.Remove(_value.GetEntityId());

//			if (Application.isPlaying)
//				UnityEngine.Object.Destroy(_value);
//			else
//				UnityEngine.Object.DestroyImmediate(_value);

//			_value = null;
//		}

//		public static void ClearAll()
//		{
//			foreach (var r in _allRefs.Values.ToArray())
//				r.DestroyInternal();

//			_allRefs.Clear();
//		}
//	}
//}