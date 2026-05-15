using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public class CommandBufferTest : MonoBehaviour
	{
		//private void Awake()
		//{
		//	var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		//	cube.transform.position = Vector3.forward * 1f;
		//	cube.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { color = Color.green };
		//}

		void Start()
		{
			AssetConfiguration.Initialize();
			ResourceSerializer.Initialise();

			if (ResourceManager.database == null)
			{
				Debug.LogError("Failed to load content data from levels.json / definitions.json!");
				return;
			}

			var ICON_SIZE = 192;
			var MAXIMUM_RENDER_TEXTURE_SIZE = 4096;
			var COLUMNS = (MAXIMUM_RENDER_TEXTURE_SIZE - ICON_SIZE * 2) / ICON_SIZE;
			var filteredDefs = ResourceManager.Definitions.Where(d => !d.IsDefaultEquivalent()).ToList();
			var atlas = new IconAtlas(ICON_SIZE, COLUMNS, filteredDefs, includeGround: false, background: null, yaw: 215f, pitch: 30f);

			GetComponent<RawImage>().texture = atlas.Texture;
		}

		//private void Update()
		//{
		//	var ICON_SIZE = 256;
		//	var MAXIMUM_RENDER_TEXTURE_SIZE = 2048;
		//	var COLUMNS = (MAXIMUM_RENDER_TEXTURE_SIZE - ICON_SIZE * 2) / ICON_SIZE;
		//	var filteredDefs = ResourceManager.Definitions.Where(d => !d.IsDefaultEquivalent()).ToList();
		//	filteredDefs.RemoveRange(COLUMNS * 2, filteredDefs.Count - COLUMNS * 2);
		//	var atlas = new IconAtlas(ICON_SIZE, COLUMNS, filteredDefs, includeGround: false, background: null, yaw: 35f, pitch: 30f);

		//	GetComponent<RawImage>().texture = atlas.Texture;
		//}
	}
}
