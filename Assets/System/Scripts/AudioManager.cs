using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MassiveHadronLtd
{
	public class AudioManager : MonoBehaviour
	{
		[SerializeField] private AudioMixer mixer;
		[SerializeField] private int maxSoundChannels = 16;

		private static AudioManager instance;
		private List<AudioSource> soundPool;

		// Track current music to stop previous
		private AudioSource currentMusicSource;

		private void Awake()
		{
			if (instance != null && instance != this)
			{
				Destroy(gameObject);
				return;
			}

			instance = this;
			DontDestroyOnLoad(gameObject);

			soundPool = new List<AudioSource>();
			for (int i = 0; i < maxSoundChannels; i++)
			{
				var source = gameObject.AddComponent<AudioSource>();
				source.playOnAwake = false;
				if (mixer != null)
					source.outputAudioMixerGroup = mixer.FindMatchingGroups("Master")[0];
				soundPool.Add(source);
			}
		}

		// === SOUND EFFECTS ===
		public static void PlaySound(AudioClip clip, float volume = 1f, float pitch = 1f)
		{
			if (clip == null || instance == null) return;

			var source = instance.soundPool.Find(s => !s.isPlaying);
			if (source == null) source = instance.soundPool[0];

			source.clip = clip;
			source.volume = volume;
			source.pitch = pitch;
			source.Play();
		}

		// === MUSIC — OVERLOAD 1: String name (convenient) ===
		public static AudioSource PlayMusic(string clipName, bool loop = true)
		{
			if (instance == null) return null;

			// Stop previous music
			if (instance.currentMusicSource != null)
			{
				instance.currentMusicSource.Stop();
				Destroy(instance.currentMusicSource);
			}

			if (string.IsNullOrEmpty(clipName))
				return null;

			var clip = ResourceResolvers.MusicResolver?.Find(clipName);
			if (clip == null)
			{
				Debug.LogWarning($"AudioManager: Music clip '{clipName}' not found.");
				return null;
			}

			return PlayMusicClip(clip, loop);
		}

		// === MUSIC — OVERLOAD 2: Pre-loaded AudioClip (explicit, retains your old pattern) ===
		public static AudioSource PlayMusic(AudioClip clip, bool loop = true)
		{
			if (instance == null) return null;

			// Stop previous music
			if (instance.currentMusicSource != null)
			{
				instance.currentMusicSource.Stop();
				Destroy(instance.currentMusicSource);
			}

			if (clip == null) return null;

			return PlayMusicClip(clip, loop);
		}

		// Internal shared logic
		private static AudioSource PlayMusicClip(AudioClip clip, bool loop)
		{
			var musicSource = instance.gameObject.AddComponent<AudioSource>();
			musicSource.clip = clip;
			musicSource.loop = loop;
			musicSource.Play();

			instance.currentMusicSource = musicSource;
			return musicSource;
		}

		public static void StopMusic()
		{
			if (instance == null || instance.currentMusicSource == null) return;

			instance.currentMusicSource.Stop();
			Destroy(instance.currentMusicSource);
			instance.currentMusicSource = null;
		}
	}
}
