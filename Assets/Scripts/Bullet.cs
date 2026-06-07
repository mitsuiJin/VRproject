using UnityEngine;

public class Bullet : MonoBehaviour
{
    // 총알이 좀비의 Collider(Trigger)와 충돌했을 때 유니티가 자동으로 실행하는 함수
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();

            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(1);
            }

            Destroy(gameObject);
        }
    }
}