using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class Phase
{
    public string phaseName = "Phase";
    public GameObject[] zombiePrefabs;
    public int zombieCount = 5;
    public float spawnInterval = 3f;
}

public class ZombieSpawner : MonoBehaviour
{
    public Phase[] phases;
    public float timeBetweenPhases = 5f;

    [Header("Population")]
    public int targetActiveZombies = 6;
    public float respawnDelay = 0.5f;
    public float populationCheckInterval = 0.25f;

    [Header("Spawn Area")]
    public float spawnRadius = 3f;

    private readonly List<EnemyHealth> activeZombies = new List<EnemyHealth>();

    private void Start()
    {
        FillPopulation();
        StartCoroutine(MaintainPopulation());
    }

    private IEnumerator MaintainPopulation()
    {
        WaitForSeconds checkWait = new WaitForSeconds(Mathf.Max(0.05f, populationCheckInterval));

        while (true)
        {
            RemoveDeadOrMissingZombies();

            if (activeZombies.Count < targetActiveZombies)
            {
                float delay = Mathf.Max(0f, respawnDelay);
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }

                FillPopulation();
            }

            yield return checkWait;
        }
    }

    private void FillPopulation()
    {
        RemoveDeadOrMissingZombies();

        while (activeZombies.Count < targetActiveZombies)
        {
            if (!SpawnZombie())
            {
                break;
            }
        }
    }

    private bool SpawnZombie()
    {
        GameObject zombiePrefab = GetRandomZombiePrefab();
        if (zombiePrefab == null)
        {
            Debug.LogWarning("[ZombieSpawner] No zombie prefab is assigned.");
            return false;
        }

        GameObject zombieObject = Instantiate(zombiePrefab, GetSafeSpawnPosition(), transform.rotation);
        EnemyHealth enemyHealth = zombieObject.GetComponentInChildren<EnemyHealth>();

        if (enemyHealth == null)
        {
            Debug.LogWarning($"[ZombieSpawner] Spawned '{zombieObject.name}' without EnemyHealth.");
            return false;
        }

        activeZombies.Add(enemyHealth);
        return true;
    }

    private GameObject GetRandomZombiePrefab()
    {
        int validPrefabCount = 0;

        if (phases != null)
        {
            foreach (Phase phase in phases)
            {
                if (phase == null || phase.zombiePrefabs == null)
                {
                    continue;
                }

                foreach (GameObject prefab in phase.zombiePrefabs)
                {
                    if (prefab != null)
                    {
                        validPrefabCount++;
                    }
                }
            }
        }

        if (validPrefabCount == 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, validPrefabCount);

        foreach (Phase phase in phases)
        {
            if (phase == null || phase.zombiePrefabs == null)
            {
                continue;
            }

            foreach (GameObject prefab in phase.zombiePrefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return prefab;
                }

                selectedIndex--;
            }
        }

        return null;
    }

    private void RemoveDeadOrMissingZombies()
    {
        for (int i = activeZombies.Count - 1; i >= 0; i--)
        {
            EnemyHealth enemyHealth = activeZombies[i];
            if (enemyHealth == null || enemyHealth.isDead)
            {
                activeZombies.RemoveAt(i);
            }
        }
    }

    private Vector3 GetSafeSpawnPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 randomPosition = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (NavMesh.SamplePosition(randomPosition, out NavMeshHit hit, spawnRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return transform.position;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}