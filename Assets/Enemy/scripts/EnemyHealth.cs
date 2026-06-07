using UnityEngine;
using UnityEngine.AI;
using System.Collections; // 🚨 코루틴을 위해 필수!

public class EnemyHealth : MonoBehaviour
{
    public int health = 2;
    private Animator animator;
    private NavMeshAgent agent;
    private EnemyMoovement movement;
    private CapsuleCollider coll;
    public bool isDead = false;

    [Header("테스트용 텐션 제어기")]
    [Range(0f, 1f)]
    public float currentTension = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        movement = GetComponent<EnemyMoovement>();
        coll = GetComponent<CapsuleCollider>();
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        if (currentTension >= 0.5f)
        {
            TensionAoEDeath();
        }
        else
        {
            health -= damage;
            if (health > 0)
            {
                animator.SetTrigger("Hit");
            }
            else
            {
                Die();
            }
        }
    }

    private void TensionAoEDeath()
    {
        float radius = currentTension * 8f;

        Collider[] colliders = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in colliders)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyHealth neighborHealth = hit.GetComponent<EnemyHealth>();
                if (neighborHealth != null && !neighborHealth.isDead)
                {
                    neighborHealth.ApplyKnockbackAndDie(transform.position, currentTension);
                }
            }
        }
    }

    // 💥 냉동참치 탈출! 시네마틱 애니메이션 넉백 적용
    public void ApplyKnockbackAndDie(Vector3 explosionCenter, float tensionLevel)
    {
        if (isDead) return;
        isDead = true;

        // AI 끄기
        agent.enabled = false;
        movement.enabled = false;
        coll.enabled = false; // 길막 방지를 위해 콜라이더는 꺼줍니다.

        // 🚨 냉동참치 방지: 애니메이터를 끄지 않고, 멋진 Die 애니메이션을 실행합니다!
        animator.SetTrigger("Die");

        // 물리 엔진(Rigidbody) 끄기 (통나무처럼 자빠지는 것 방지)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // 밀려날 방향 계산 (내 위치 - 폭발 위치)
        Vector3 pushDirection = (transform.position - explosionCenter).normalized;
        pushDirection.y = 0f; // 땅바닥을 따라 평행하게 밀리도록 Y축 고정

        // 텐션 값에 따라 뒤로 밀려날 '거리' 계산 (최대 3미터)
        float pushDistance = tensionLevel * 3f;

        // 애니메이션 재생과 동시에 뒤로 슬라이딩 시작!
        StartCoroutine(SlideBackwards(pushDirection, pushDistance));

        Destroy(gameObject, 3f);
    }

    // 🎬 애니메이션 재생 시간에 맞춰 자연스럽게 뒤로 미끄러지는 함수
    private IEnumerator SlideBackwards(Vector3 direction, float distance)
    {
        // 🚨 기존 0.3초는 너무 빨랐습니다! 쓰러지는 애니메이션 길이에 맞춰 1.0초로 늘립니다.
        float duration = 1.0f;
        float elapsedTime = 0f;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction * distance;

        while (elapsedTime < duration)
        {
            // 그냥 밋밋하게 밀리지 않고, 처음엔 확 밀리다가 끝에서 부드럽게 감속하는 공식 적용
            float t = elapsedTime / duration;
            float smoothT = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        agent.enabled = false;
        movement.enabled = false;
        coll.enabled = false;
        animator.SetTrigger("Die");
        Destroy(gameObject, 3f);
    }
}