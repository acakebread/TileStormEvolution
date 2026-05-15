using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm.Assets
{
	internal sealed class TextureResourceResolver : IResourceResolver<Texture>
	{
		public Texture Find(string identifier) => TextureAssets.Find(identifier);
	}

	internal sealed class SkyboxResourceResolver : IResourceResolver<Material>
	{
		public Material Find(string identifier) => SkyboxAssets.Find(identifier);
	}

	internal sealed class MusicResourceResolver : IResourceResolver<AudioClip>
	{
		public AudioClip Find(string identifier) => MusicAssets.Find(identifier);
	}
}
