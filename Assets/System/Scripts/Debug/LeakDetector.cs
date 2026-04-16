using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Global leak tracking utility with colored console output.
	/// Red text = suspicious leak pattern.
	/// 
	/// To disable completely: comment out or remove the #define below.
	/// </summary>
#if LEAK_DETECTOR_ENABLED
    public static class LeakDetector
    {
        private static int _renderTexturesCreated = 0;
        private static int _materialsCreated = 0;
        private static int _cubemapsCreated = 0;
        private static int _texture2DsCreated = 0;
        private static int _meshesCreated = 0;

        // Thresholds — tweak if needed
        private const int WARNING_THRESHOLD_CUBEMAP = 3;
        private const int WARNING_THRESHOLD_MATERIAL = 5;
        private const int WARNING_THRESHOLD_TEXTURE = 5;
        private const int WARNING_THRESHOLD_RT = 2;

        public static void ResetCounters(string context = null)
        {
            string msg = $"[LeakDetector] RESET COUNTERS {(context != null ? $"({context})" : "")} " +
                         $"| RTs:{_renderTexturesCreated} | Mats:{_materialsCreated} | Cubes:{_cubemapsCreated} " +
                         $"| Tex2D:{_texture2DsCreated} | Meshes:{_meshesCreated}";

            Debug.Log(msg);

            _renderTexturesCreated = _materialsCreated = _cubemapsCreated = _texture2DsCreated = _meshesCreated = 0;
        }

        /// <summary>
        /// Track creation of heavy Unity objects. Shows **red** when it looks leaky.
        /// </summary>
        public static void TrackCreation(UnityEngine.Object obj, string typeName, string context = null)
        {
            if (obj == null) return;

            long entityIdHash = obj.GetEntityId().GetHashCode();

            string fullMessage = $"[LeakDetector] CREATED {typeName} | {obj.name} | ID:{entityIdHash}";
            if (!string.IsNullOrEmpty(context))
                fullMessage += $" | Context:{context}";

            bool isSuspicious = false;

            switch (obj)
            {
                case RenderTexture _:
                    _renderTexturesCreated++;
                    if (_renderTexturesCreated > WARNING_THRESHOLD_RT) isSuspicious = true;
                    break;

                case Material _:
                    _materialsCreated++;
                    if (_materialsCreated > WARNING_THRESHOLD_MATERIAL) isSuspicious = true;
                    break;

                case Cubemap _:
                    _cubemapsCreated++;
                    if (_cubemapsCreated > WARNING_THRESHOLD_CUBEMAP) isSuspicious = true;
                    break;

                case Texture2D _:
                    _texture2DsCreated++;
                    if (_texture2DsCreated > WARNING_THRESHOLD_TEXTURE) isSuspicious = true;
                    break;

                case Mesh _:
                    _meshesCreated++;
                    break;
            }

            if (isSuspicious)
            {
                Debug.Log($"<color=red>{fullMessage}  ← SUSPICIOUS - possible leak!</color>");
            }
            else
            {
                Debug.Log(fullMessage);
            }
        }

        /// <summary>
        /// Memory snapshot with orange warnings for high counts.
        /// </summary>
        public static void LogSnapshot(string label = "Snapshot")
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            var cubemaps = Resources.FindObjectsOfTypeAll<Cubemap>();

            long texBytes = textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t));
            long cubeBytes = cubemaps.Sum(c => Profiler.GetRuntimeMemorySizeLong(c));

            Debug.Log($"[{label}] Textures: {textures.Length} | {texBytes / (1024f * 1024f):F2} MB");
            Debug.Log($"[{label}] Cubemaps: {cubemaps.Length} | {cubeBytes / (1024f * 1024f):F2} MB");

            var biggest = textures.OrderByDescending(t => Profiler.GetRuntimeMemorySizeLong(t)).Take(8);
            foreach (var t in biggest)
            {
                long size = Profiler.GetRuntimeMemorySizeLong(t);
                Debug.Log($"   → {t.name} ({t.width}x{t.height}) - {size / (1024f * 1024f):F2} MB");
            }

            if (cubemaps.Length > 12)
                Debug.Log($"<color=orange>[{label}] WARNING: High cubemap count ({cubemaps.Length}) — likely leak in ReflectionEffectCamera</color>");

            if (textures.Length > 1300)
                Debug.Log($"<color=orange>[{label}] WARNING: Very high texture count ({textures.Length}) — check for undestroyed materials/textures</color>");
        }
    }
#else
	// Empty dummy implementation when leak detection is disabled
	// All calls to LeakDetector.xxx() will be compiled out (zero cost)
	public static class LeakDetector
	{
		public static void ResetCounters(string context = null) { }
		public static void TrackCreation(UnityEngine.Object obj, string typeName, string context = null) { }
		public static void LogSnapshot(string label = "Snapshot") { }
	}
#endif
}