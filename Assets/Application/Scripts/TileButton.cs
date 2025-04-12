// Copyright 2022 massivehadron.com ltd. created 03/10/2022 by Andrew Cakebread

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	public class TileButton : MonoBehaviour
	{
		//PlayableDirector director => FindObjectOfType<PlayableDirector>();

		private PlayableDirector _director = null;
		private PlayableDirector director
		{
			get
			{
				if (null == _director) _director = GameObject.Find("AssemblySequence").GetComponent<PlayableDirector>();
				return _director;
			}
		}

		void Start()
		{
			transform.parent.Find("label").GetComponent<TMPro.TMP_Text>().text = GetComponent<RawImage>().mainTexture.name;

			GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
			{
				foreach (var matController in FindObjectsOfType<MaterialController>(true))
				{
					var texture = GetComponent<RawImage>().texture;
					matController.opaque.mainTexture = texture;
					matController.transparent.mainTexture = texture;
					GameObject.Find("currentTile").GetComponent<TMPro.TMP_Text>().text = texture.name;
				}
				director.Stop();
				director.Play();
			});
		}
	}
}