using UnityEngine;
using UnityEngine.UI;

public class textureTest : MonoBehaviour
{
    public RawImage image;

    void Start()
    {
		image.texture = MassiveHadronLtd.TextureUtils.GenerateXorTexture256();
	}
}
