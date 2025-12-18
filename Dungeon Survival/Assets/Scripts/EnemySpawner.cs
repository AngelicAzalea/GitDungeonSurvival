using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Tilemaps;

public class EnemySpawner : MonoBehaviour
{
	[SerializeField] private Transform[] spawnPoints;
	[SerializeField] private float timeBetweenSpawns = 5.0f;
	private float timeSinceLastSpawn;

	[Header("Prefabs")]
	[Tooltip("One or more enemy prefabs that can be spawned. If more than one is provided, a random prefab will be chosen for each creation.")]
	[SerializeField] private EnemyBaseClass[] enemyPrefabs;
	private IObjectPool<EnemyBaseClass> enemyPool;

	[Header("References")]
	[Tooltip("Tile generator used to pick safe spawn tiles")]
	[SerializeField] private TileGeneratorScript tileGenerator;
	[Tooltip("Player transform to measure distance from when spawning")]
	[SerializeField] private Transform playerTarget;
	[Tooltip("Minimum distance in tile cells from player when picking spawn tile")]
	[SerializeField] private int minSpawnDistanceInCells = 6;

	private void Awake()
	{
		// Create an ObjectPool for EnemyBaseClass instances.
		enemyPool = new ObjectPool<EnemyBaseClass>(
			CreateEnemy,
			OnGetEnemy,
			OnReleaseEnemy,
			OnDestroyEnemy,
			collectionCheck: true,
			defaultCapacity: 10,
			maxSize: 1000
		);
	}

	private EnemyBaseClass CreateEnemy()
	{
		// pick a random prefab from the list
		if (enemyPrefabs == null || enemyPrefabs.Length == 0)
		{
			Debug.LogError("EnemySpawner: No enemy prefabs configured.");
			return null;
		}

		var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
		var enemy = Instantiate(prefab);
		// start inactive; spawner will activate after positioning and OnSpawn
		enemy.gameObject.SetActive(false);
		// subscribe once so enemy can notify spawner when it dies
		enemy.OnDeath += ReleaseEnemy;
		return enemy;
	}

	private void OnGetEnemy(EnemyBaseClass enemy)
	{
		// Prepare reusable state but don't enable yet � spawner will position and call OnSpawn then enable.
		if (enemy != null) enemy.ResetForReuse();
	}

	private void OnReleaseEnemy(EnemyBaseClass enemy)
	{
		// ensure the enemy is deactivated when returned
		if (enemy != null) enemy.gameObject.SetActive(false);
	}

	private void OnDestroyEnemy(EnemyBaseClass enemy)
	{
		if (enemy != null)
			Destroy(enemy.gameObject);
	}

	// Update is called once per frame
	void Update()
	{
		if ((spawnPoints == null || spawnPoints.Length == 0) && tileGenerator == null)
			return; // nothing to spawn into

		if (enemyPrefabs == null || enemyPrefabs.Length == 0)
			return;

		if (playerTarget == null)
			Debug.LogWarning("EnemySpawner: playerTarget is not assigned. Enemies will not get a player reference.");

		if (Time.time >= timeSinceLastSpawn)
		{
			// Spawn Enemy
			timeSinceLastSpawn = Time.time + timeBetweenSpawns;

			// Get an enemy from the pool (OnGet callback executed now)
			var enemy = enemyPool.Get();
			if (enemy == null) return;

			// Determine spawn position. Prefer tile generator safe tile away from player when available.
			Vector3 spawnWorldPos = Vector3.zero;
			bool positioned = false;
			if (tileGenerator != null && playerTarget != null)
			{
				positioned = tileGenerator.TryGetSpawnPositionAwayFrom(playerTarget.position, minSpawnDistanceInCells, out spawnWorldPos);
			}

			if (!positioned && spawnPoints != null && spawnPoints.Length > 0)
			{
				var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
				spawnWorldPos = spawnPoint.position;
				positioned = true;
			}

			if (!positioned)
			{
				// fallback to spawner position
				spawnWorldPos = transform.position;
			}

			enemy.transform.position = spawnWorldPos;

			// Let enemy set up refs/decisions based on player target and pathfinder tilemaps
			Tilemap collisionMap = tileGenerator != null ? tileGenerator.CollisionTilemap : null;
			Tilemap nonCollisionMap = tileGenerator != null ? tileGenerator.NonCollisionTilemap : null;
			enemy.OnSpawn(playerTarget, collisionMap, nonCollisionMap);

			// Apply GameManager difficulty scaling immediately so spawned enemies reflect current difficulty
			if (GameManager.Instance != null)
			{
				enemy.ApplyScalingFromGameMultiplier();
			}

			// Finally activate enemy GameObject so its logic runs
			enemy.gameObject.SetActive(true);

			// Optional: call PrepareForSpawn if the enemy expects any initial delay/setup
			enemy.PrepareForSpawn();
		}
	}

	// Public helper to release an enemy back to the pool (can be called by EnemyBaseClass on death)
	public void ReleaseEnemy(EnemyBaseClass enemy)
	{
		if (enemy == null) return;

		if (enemyPool != null)
			enemyPool.Release(enemy);
		else
			Destroy(enemy.gameObject);
	}
}
