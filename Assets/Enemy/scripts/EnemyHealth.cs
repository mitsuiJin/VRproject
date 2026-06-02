using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    private Animator animator;
    private CapsuleCollider capCollider;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        capCollider = GetComponent<CapsuleCollider>();
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        UnityEngine.Debug.Log("좀비 피격! 현재 체력: " + currentHealth);

        // 방금 만든 'Hit' 트리거를 작동시켜 피격 애니메이션 재생!
        animator.SetTrigger("Hit");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        UnityEngine.Debug.Log("좀비 사망!");

        // 방금 만든 'Die' 트리거를 작동시켜 사망 애니메이션 재생!
        animator.SetTrigger("Die");

        if (capCollider != null) capCollider.enabled = false;

        if (GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
        {
            GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        }

        Destroy(gameObject, 3.0f);
    }
}