using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class SparkController : MonoBehaviour
	{
		[System.Serializable]
		public class SparkSettings
		{
			public float speed = 4f;
			public float lifetime = 1f;
			public float width = 0.02f; // Controls size for simple particles and body width for three-zone
			public bool decay = true; // Shrink width with age
			public Color color = Color.white;
			public float gravity = 10f; // Y-axis damping
			public float moveScale = 1f; // Velocity scale
			public float bounceDamping = 0.8f; // Velocity damping on collision
			public float groundHeight = 0f; // Ground plane Y position
			public bool useGlobalGroundPlane = true;
		}

		[SerializeField] private Material particleMaterial; // Material for ParticleSystem
		[SerializeField] private bool useThreeZoneSlicing = false;
		[SerializeField] private bool useAdditiveBlending = true; // Moved here for initialization
		[SerializeField] private SparkSettings defaultSettings;
		[SerializeField] private bool updateSparks = true; // Controls whether sparks are updated
		private readonly float simSpeed = 1f;
		private ParticleSystem customParticleSystem;

		private class SparkData
		{
			public int poolIndex; // ParticleSystem pool index
			public Vector3 position; // World space
			public Vector3 velocity; // World space
			public float lifetime;
			public float maxLifetime;
			public Color color;
			public float width;
			public float initialWidth;
			public float tipSize;
			public bool isActive;
		}

		private List<SparkData> activeSparks;

		void Awake()
		{
			activeSparks = new List<SparkData>();
			if (particleMaterial == null)
			{
				Debug.LogError("SparkController: particleMaterial is not assigned!");
				enabled = false;
				return;
			}

			customParticleSystem = new ParticleSystem(particleMaterial, useThreeZoneSlicing, useAdditiveBlending);
		}

		void FixedUpdate()
		{
			if (updateSparks)
				UpdateSparks();
		}

		void Update()
		{
			customParticleSystem.Render();
		}

		public void SpawnSpark(Vector3 position, Vector3 velocity, SparkSettings settings = null)
		{
			if (settings == null) settings = defaultSettings;

			// Convert SparkSettings to ParticleSystem.ParticleSettings
			var particleSettings = new ParticleSystem.ParticleSettings
			{
				lifetime = settings.lifetime,
				width = settings.width,
				decay = settings.decay,
				color = settings.color
			};

			int poolIndex = customParticleSystem.SpawnParticle(position, velocity * settings.speed, particleSettings);
			if (poolIndex == -1) return;

			SparkData spark = new SparkData
			{
				poolIndex = poolIndex,
				position = position, // World space
				velocity = velocity * settings.speed, // World space, scaled
				lifetime = settings.lifetime,
				maxLifetime = settings.lifetime,
				color = settings.color,
				width = settings.width,
				initialWidth = settings.width,
				tipSize = settings.width / 2f, // Tip size is half of width
				isActive = true
			};

			activeSparks.Add(spark);
		}

		private void UpdateSparks()
		{
			float deltaTime = Time.deltaTime;

			for (int i = activeSparks.Count - 1; i >= 0; i--)
			{
				SparkData spark = activeSparks[i];
				if (!spark.isActive)
				{
					activeSparks.RemoveAt(i);
					continue;
				}

				spark.lifetime -= deltaTime;

				if (spark.lifetime <= 0f)
				{
					spark.isActive = false;
					activeSparks.RemoveAt(i);
					customParticleSystem.UpdateParticle(spark.poolIndex, spark.position, spark.velocity, 0f, spark.width, spark.tipSize, spark.color);
					continue;
				}

				if (!useAdditiveBlending)
				{
					spark.color.a = spark.lifetime / spark.maxLifetime;
				}
				else
				{
					spark.color.a = 1f; // Alpha irrelevant for additive
				}

				if (defaultSettings.decay)
				{
					float decayFactor = spark.lifetime / spark.maxLifetime;
					spark.width = spark.initialWidth * decayFactor;
					spark.tipSize = spark.width / 2f; // Keep tipSize as half of width
				}

				spark.velocity.y -= defaultSettings.gravity * deltaTime * simSpeed;
				spark.position += spark.velocity * defaultSettings.moveScale * deltaTime * simSpeed;

				// Ground collision
				float currentY = spark.position.y; // Always use world space
				float groundY = defaultSettings.groundHeight;
				if (!defaultSettings.useGlobalGroundPlane)
				{
					groundY = transform.position.y + defaultSettings.groundHeight;
				}

				if (spark.velocity.y < 0 && currentY <= groundY)
				{
					spark.position.y = groundY;
					spark.velocity.y = -spark.velocity.y * defaultSettings.bounceDamping;
				}

				// Update the ParticleSystem with the new spark properties
				customParticleSystem.UpdateParticle(spark.poolIndex, spark.position, spark.velocity, spark.lifetime, spark.width, spark.tipSize, spark.color);
			}
		}
	}
}