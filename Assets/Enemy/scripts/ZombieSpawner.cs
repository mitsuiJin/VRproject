using System.Collections;
using UnityEngine;
using UnityEngine.AI; // NavMesh 기능 사용을 위해 추가

[System.Serializable]
public class Phase
{
    public string phaseName = "페이즈 이름";
    public GameObject[] zombiePrefabs;
    public int zombieCount = 5;
    public float spawnInterval = 3f;
}

public class ZombieSpawner : MonoBehaviour
{
    public Phase[] phases;
    public float timeBetweenPhases = 5f;

    // 스폰 반경 설정 (이 반경 안에서 랜덤하게 소환됩니다)
    [Header("스폰 설정")]
    public float spawnRadius = 3f;

    private int currentPhaseIndex = 0;

    void Start()
    {
        StartCoroutine(StartNextPhase());
    }

    IEnumerator StartNextPhase()
    {
        if (currentPhaseIndex < phases.Length)
        {
            Phase currentPhase = phases[currentPhaseIndex];
            UnityEngine.Debug.Log($"<color=red>--- {currentPhase.phaseName} 시작! ---</color>");

            for (int i = 0; i < currentPhase.zombieCount; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, currentPhase.zombiePrefabs.Length);
                GameObject zombieToSpawn = currentPhase.zombiePrefabs[randomIndex];

                // 랜덤 오프셋 계산 및 안전한 위치 탐색
                Vector3 spawnPosition = GetSafeSpawnPosition();

                Instantiate(zombieToSpawn, spawnPosition, transform.rotation);

                yield return new WaitForSeconds(currentPhase.spawnInterval);
            }

            UnityEngine.Debug.Log($"{currentPhase.phaseName} 좀비 출현 완료. 다음 페이즈 대기 중...");
            yield return new WaitForSeconds(timeBetweenPhases);

            currentPhaseIndex++;
            StartCoroutine(StartNextPhase());
        }
        else
        {
            UnityEngine.Debug.Log("<color=blue>모든 페이즈 종료! 방어 성공!</color>");
        }
    }

    // 반경 내에서 안전한 NavMesh 위치를 찾는 함수
    Vector3 GetSafeSpawnPosition()
    {
        // 1. 반경 내의 랜덤한 원형 좌표(X, Z)를 구합니다.
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spawnRadius;
        Vector3 randomPos = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

        // 2. 그 랜덤 위치에서 가장 가까운 NavMesh(걸을 수 있는 바닥)를 찾습니다.
        NavMeshHit hit;
        // 반경(spawnRadius) 내에서 유효한 바닥을 찾으면 그 위치를 반환
        if (NavMesh.SamplePosition(randomPos, out hit, spawnRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // 만약 안전한 곳을 찾지 못했다면(거의 일어나지 않음) 포탈 중앙을 반환합니다.
        return transform.position;
    }

    // 씬 뷰(Scene View)에서 스폰 반경을 시각적으로 보여주는 기능
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}