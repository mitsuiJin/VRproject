using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public int health = 2;
    private Animator animator;
    private NavMeshAgent agent;
    private EnemyMoovement movement;
    private CapsuleCollider coll;
    public bool isDead = false;

    private EnemySound enemySound;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        movement = GetComponent<EnemyMoovement>();
        coll = GetComponent<CapsuleCollider>();
        enemySound = GetComponent<EnemySound>();
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        if (enemySound != null) enemySound.PlayHitSound();

        // 플레이어 상태 스크립트(PlayerStatus)의 Instance가 존재하는지 검증
        float playerTension = 0f;
        if (PlayerStatus.Instance != null)
        {
            playerTension = PlayerStatus.Instance.currentTension;
        }

        // 플레이어의 긴장 상태가 0.5 이상일 경우 범위 공격 및 즉사 처리
        if (playerTension >= 0.5f)
        {
            TensionAoEDeath(playerTension);
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

    private void TensionAoEDeath(float tensionLevel)
    {
        // 플레이어의 긴장도 수치에 비례하여 범위 반경 결정
        float radius = tensionLevel * 8f;
        Collider[] colliders = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider hit in colliders)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyHealth neighborHealth = hit.GetComponent<EnemyHealth>();

                if (neighborHealth != null && !neighborHealth.isDead)
                {
                    neighborHealth.ApplyKnockbackAndDie(transform.position, tensionLevel);
                }
            }
        }
    }

    public void ApplyKnockbackAndDie(Vector3 explosionCenter, float tensionLevel)
    {
        if (isDead) return;
        isDead = true;

        if (enemySound != null) enemySound.PlayDeathSound();

        agent.enabled = false;
        movement.enabled = false;
        coll.enabled = false;

        animator.SetTrigger("Die");

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Vector3 pushDirection = (transform.position - explosionCenter).normalized;
        pushDirection.y = 0f;
        float pushDistance = tensionLevel * 3f;

        StartCoroutine(SlideBackwards(pushDirection, pushDistance));
        Destroy(gameObject, 3f);
    }

    private IEnumerator SlideBackwards(Vector3 direction, float distance)
    {
        float duration = 1.0f;
        float elapsedTime = 0f;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction * distance;

        while (elapsedTime < duration)
        {
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

        if (enemySound != null) enemySound.PlayDeathSound();

        agent.enabled = false;
        movement.enabled = false;
        coll.enabled = false;

        animator.SetTrigger("Die");
        Destroy(gameObject, 3f);
    }
}