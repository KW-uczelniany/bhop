using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public Transform target;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = 7.0f; // Increased base speed of the enemy to be much faster
        
        StartCoroutine(IncreaseSpeedRoutine());
        
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                target = player.transform;
        }
    }

    void Update()
    {
        if (target != null && agent.isOnNavMesh)
        {
            agent.SetDestination(target.position);
        }
    }

    IEnumerator IncreaseSpeedRoutine()
    {
        while (true)
        {
            // Gains 0.5 speed every 5 seconds instead of 0.1 every 10 seconds
            yield return new WaitForSeconds(5f);
            if (agent != null)
            {
                agent.speed += 0.5f;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.GameOver();
            }
        }
    }
}
