using UnityEngine;
using UnityEngine.AI;

public class EnemyMoovement : MonoBehaviour
{
    private Transform target;
    private NavMeshAgent agent;
    private Animator animator;
    private EnemyHealth health;

    public float attackCooldown = 2.0f;
    private float nextAttackTime = 0f;

    /* 애니메이션 파라미터 이름을 인스펙터에서 설정할 수 있도록 변수로 빼냅니다.
       기본값은 "IsWalking"으로 설정해 둡니다. */
    public string moveParameterName = "IsWalking";
    public string attackParameterName = "Attack";

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
        }
    }

    void Update()
    {
        if (health != null && agent.enabled == false) return;
        if (target == null) return;
        if (!agent.isOnNavMesh) return;

        agent.SetDestination(target.position);

        if (agent.pathPending) return;

        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            animator.SetBool(moveParameterName, false);

            Vector3 direction = (target.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

            if (Time.time >= nextAttackTime)
            {
                animator.SetTrigger(attackParameterName);
                UnityEngine.Debug.Log("좀비 공격!");

                nextAttackTime = Time.time + attackCooldown;
            }
        }
        else
        {
            // 플레이어가 도망쳐서 거리가 멀어진 경우
            animator.SetBool(moveParameterName, true);

            /* [공격 중단 로직 추가] 
               Any State -> Attack으로 가는 트리거 신호가 추격 도중에 완전히 리셋되도록 
               ResetTrigger를 호출하여 예약된 공격 동작을 지워줍니다. */
            animator.ResetTrigger(attackParameterName);
        }
    }
}