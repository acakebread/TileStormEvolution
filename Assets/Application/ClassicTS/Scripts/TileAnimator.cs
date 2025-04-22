using UnityEngine;
using GameDatabase;
using System.Linq;

namespace GamePreviewNamespace
{
	public class TileAnimator : MonoBehaviour
	{
		private MeshRenderer _renderer;
		private DatabaseLoader.TextureSet textureSet;
		private Texture2D[] textures;
		private float[] durations;
		private int currentFrame;
		private float timer;

		public void Initialize(DatabaseLoader.TextureSet ts)
		{
			textureSet = ts;
			_renderer = GetComponentInChildren<MeshRenderer>();
			if (_renderer == null || textureSet == null || textureSet.frames == null || textureSet.frames.Length == 0)
			{
				Destroy(this);
				return;
			}

			// Load all textures
			textures = new Texture2D[textureSet.frames.Length];
			durations = new float[textureSet.frames.Length];
			bool hasValidTextures = false;

			for (int i = 0; i < textureSet.frames.Length; i++)
			{
				var frame = textureSet.frames[i];
				durations[i] = frame.fDuration > 0 ? frame.fDuration : 1f; // Default to 1s if 0
				string texPath = $"{PreviewSettings.TexturePath}{frame.szTexture}".Replace(".tga", "").Replace(".png", "");
				textures[i] = Resources.Load<Texture2D>(texPath);
				if (textures[i] != null)
				{
					hasValidTextures = true;
				}
				else
				{
					Debug.LogWarning($"Texture not found: {texPath} for frame {frame.name}");
				}
			}

			if (!hasValidTextures || textureSet.frames.Length == 1)
			{
				// Apply first valid texture and disable animation
				for (int i = 0; i < textures.Length; i++)
				{
					if (textures[i] != null)
					{
						ApplyTexture(i);
						break;
					}
				}
				Destroy(this);
				return;
			}

			// Apply first frame
			currentFrame = 0;
			timer = 0;
			ApplyTexture(currentFrame);
		}

		private void ApplyTexture(int index)
		{
			if (_renderer != null && textures[index] != null)
			{
				if (_renderer.material == null)
				{
					_renderer.material = new Material(Shader.Find("Standard"));
				}
				_renderer.material.mainTexture = textures[index];
			}
		}

		void Update()
		{
			if (textures == null || textures.Length <= 1)
				return;

			timer += Time.deltaTime;
			if (timer >= durations[currentFrame])
			{
				timer -= durations[currentFrame];
				currentFrame = (currentFrame + 1) % textures.Length;
				ApplyTexture(currentFrame);
			}
		}

		void OnDestroy()
		{
			if (_renderer != null && _renderer.material != null)
			{
				Destroy(_renderer.material);
			}
		}

		public static DatabaseLoader.TextureSet GetTextureSetForTileDef(DatabaseLoader.TileDef tileDef)
		{
			DatabaseLoader.Theme theme = DatabaseLoader.instance.Themes.FirstOrDefault(t => t.name == tileDef.szTheme || t.szTileTextureSet == tileDef.szTheme);
			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
			{
				DatabaseLoader.TextureSet texSet = DatabaseLoader.instance.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
				if (texSet != null && texSet.frames != null && texSet.frames.Length > 0)
				{
					//Debug.Log($"TextureSet found: {texSet.name}, frames={texSet.frames.Length}");
					return texSet;
				}
			}
			//Debug.LogWarning($"No TextureSet for theme={tileDef.szTheme}");
			return null;
		}
	}
}