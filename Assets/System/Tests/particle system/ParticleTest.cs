using MassiveHadronLtd;
using System.Collections;
using UnityEngine;

public class ParticleTest : MonoBehaviour
{
	[SerializeField] private SparkController sparkController;
	[SerializeField][Range(0, 10)] private float delay = 0.1f;
	[SerializeField][Range(1, 128)] private int ct = 1;
	[SerializeField] private bool continuous = true;
	[SerializeField] private Vector3 spawnOffset = Vector3.zero;
	[SerializeField][Range(0, 2)] private float lifetimeVariation = 0.5f; // New: Random lifetime variation
	[SerializeField] private Vector3 bias = Vector3.zero;

	IEnumerator Start()
	{
		while (true)
		{
			if (continuous || Input.GetKey(KeyCode.Space))
			{
				for (var n = 0; n < ct; ++n)
				{
					var vel = Random.onUnitSphere * Random.value * 0.5f;
					vel.y *= 2f; // Exaggerate vertical speed
					vel += bias;
					Vector3 worldPos = sparkController.transform.position + spawnOffset;

					// Create custom settings with lifetime variation
					var settings = new SparkController.SparkSettings
					{
						speed = sparkController.defaultSettings.speed,
						lifetime = sparkController.defaultSettings.lifetime,
						lifetimeVariation = lifetimeVariation, // Use Inspector value
						width = sparkController.defaultSettings.width,
						decay = sparkController.defaultSettings.decay,
						color = sparkController.defaultSettings.color,
						gravity = sparkController.defaultSettings.gravity,
						moveScale = sparkController.defaultSettings.moveScale,
						bounceDamping = sparkController.defaultSettings.bounceDamping,
						groundHeight = sparkController.defaultSettings.groundHeight,
						useGlobalGroundPlane = sparkController.defaultSettings.useGlobalGroundPlane
					};

					sparkController.SpawnSpark(worldPos, vel, settings);
				}
			}
			yield return new WaitForSeconds(delay);
		}
	}
}