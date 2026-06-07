using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    // 싱글톤 패턴을 적용하여 전역에서 쉽게 접근할 수 있도록 합니다.
    public static PlayerStatus Instance { get; private set; }

    [Header("Player State")]
    [Range(0f, 1f)]
    public float currentTension = 0f; // 플레이어의 현재 긴장도

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}