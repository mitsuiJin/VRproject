using System;
using UnityEngine;

public class EnemySound : MonoBehaviour
{
    private AudioSource audioSource;
    public AudioClip hitClip;
    public AudioClip deathClip;
    public AudioClip attackClip;
    public AudioClip[] growlClips; 

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        StartCoroutine(PlayRandomGrowl());
    }

    // 으르렁거리는 소리는 랜덤하게 재생
    System.Collections.IEnumerator PlayRandomGrowl()
    {
        while (true)
        {
            if (growlClips.Length > 0)
            {
                audioSource.PlayOneShot(growlClips[UnityEngine.Random.Range(0, growlClips.Length)]);
            }
            yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 10f)); // 5~10초 간격으로 재생
        }
    }

    public void PlayHitSound() => audioSource.PlayOneShot(hitClip);
    public void PlayDeathSound() => audioSource.PlayOneShot(deathClip);
    public void PlayAttackSound() => audioSource.PlayOneShot(attackClip);
}