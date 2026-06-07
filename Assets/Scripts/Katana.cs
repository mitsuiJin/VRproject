
using UnityEngine;

public class Katana : MonoBehaviour
{
    public int damage = 1;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip sliceClip; // 칼날로 좀비를 베는 사운드 등록

    private bool isHitting = false;

    void Start()
    {
        // 오브젝트에 AudioSource가 연결되어 있지 않다면 자동으로 가져옴
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isHitting) return;

        if (other.CompareTag("Enemy"))
        {
            EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

            if (enemy != null && !enemy.isDead)
            {
                enemy.TakeDamage(damage);
                isHitting = true;

                // 좀비를 베는 효과음 재생
                if (audioSource != null && sliceClip != null)
                {
                    audioSource.PlayOneShot(sliceClip);
                }

                Debug.Log("첫 타격 성공 및 베기 사운드 재생");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            isHitting = false;
            Debug.Log("칼이 좀비 몸에서 빠져나감");
        }
    }
}