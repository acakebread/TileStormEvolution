using UnityEngine;

namespace MassiveHadronLtd
{
	public interface IResourceResolver<out T> where T : UnityEngine.Object
	{
		T Find(string identifier);
	}

	public interface IResourcePathResolver
	{
		string GetPath();
	}

	public static class ResourceResolvers
	{
		public static IResourceResolver<Texture> TextureResolver { get; set; }
		public static IResourceResolver<Material> SkyboxResolver { get; set; }
		public static IResourceResolver<AudioClip> MusicResolver { get; set; }
		public static IResourcePathResolver GeometryMaterialsPathResolver { get; set; }
	}
}
