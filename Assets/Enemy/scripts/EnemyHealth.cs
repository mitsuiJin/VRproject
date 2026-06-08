using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public int health = 2;
    public float tensionInstantKillThreshold = 0.5f;
    public float tensionKnockbackDistanceMultiplier = 3f;
    public float minimumKnockbackDistance = 1.25f;
    public float knockbackDuration = 0.45f;
    public bool enableKnockbackHaptics = true;
    [Range(0f, 1f)]
    public float knockbackHapticAmplitude = 1f;
    public float knockbackHapticDuration = 0.5f;

    private Animator animator;
    private NavMeshAgent agent;
    private EnemyMoovement movement;
    private CapsuleCollider coll;
    public bool isDead = false;

    private EnemySound enemySound;

    void Awake()
    {
        CacheComponents();
    }

    void Start()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        movement = GetComponent<EnemyMoovement>();
        coll = GetComponent<CapsuleCollider>();
        enemySound = GetComponent<EnemySound>();
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, transform.position);
    }

    public void TakeDamage(int damage, Vector3 hitPoint)
    {
        if (isDead) return;

        if (enemySound != null) enemySound.PlayHitSound();

        // 플레이어 상태 스크립트(PlayerStatus)의 Instance가 존재하는지 검증
        float playerTension = 0f;
        if (PlayerStatus.Instance != null)
        {
            playerTension = PlayerStatus.Instance.currentTension;
        }

        // High tension attacks kill immediately and push the directly hit enemy away from the hit point.
        if (playerTension >= tensionInstantKillThreshold)
        {
            ApplyKnockbackAndDie(hitPoint, playerTension);
            return;
        }

        health -= damage;
        if (health > 0)
        {
            if (animator != null) animator.SetTrigger("Hit");
        }
        else
        {
            Die();
        }
    }

    public void ApplyKnockbackAndDie(Vector3 knockbackOrigin, float tensionLevel)
    {
        if (isDead) return;
        CacheComponents();

        isDead = true;

        if (enemySound != null) enemySound.PlayDeathSound();

        if (agent != null && agent.enabled) agent.enabled = false;
        if (movement != null) movement.enabled = false;
        if (coll != null) coll.enabled = false;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetTrigger("Die");
        }

        if (enableKnockbackHaptics)
        {
            RightControllerHaptics.Pulse(knockbackHapticAmplitude, knockbackHapticDuration);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Vector3 pushDirection = transform.position - knockbackOrigin;
        pushDirection.y = 0f;

        if (pushDirection.sqrMagnitude < 0.0001f)
        {
            pushDirection = -transform.forward;
            pushDirection.y = 0f;
        }

        if (pushDirection.sqrMagnitude < 0.0001f)
        {
            pushDirection = Vector3.back;
        }

        pushDirection.Normalize();

        float pushDistance = Mathf.Max(minimumKnockbackDistance, Mathf.Clamp01(tensionLevel) * tensionKnockbackDistanceMultiplier);

        StartCoroutine(SlideBackwards(pushDirection, pushDistance));
        Destroy(gameObject, 3f);
    }

    private IEnumerator SlideBackwards(Vector3 direction, float distance)
    {
        float duration = Mathf.Max(0.01f, knockbackDuration);
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
        CacheComponents();

        isDead = true;

        if (enemySound != null) enemySound.PlayDeathSound();

        if (agent != null && agent.enabled) agent.enabled = false;
        if (movement != null) movement.enabled = false;
        if (coll != null) coll.enabled = false;

        if (animator != null) animator.SetTrigger("Die");
        Destroy(gameObject, 3f);
    }
}
