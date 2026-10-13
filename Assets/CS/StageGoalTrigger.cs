using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class StageGoalTrigger : MonoBehaviour
{
    [SerializeField] private StageManager stageManager;
    [SerializeField] private int stageToComplete = 1;
    [SerializeField] private float finishZ = 36f;
    [SerializeField] private float finishX = -21f;

    private void OnTriggerEnter(Collider other) => CheckBoulder(other);
    private void OnTriggerStay(Collider other) => CheckBoulder(other);

    private void CheckBoulder(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        Rigidbody body = other.attachedRigidbody;
        if (body == null || body.GetComponent<BoulderSlopeDrag>() == null)
            return;

        if (stageManager == null)
            stageManager = FindFirstObjectByType<StageManager>();
        if (stageManager == null)
            return;

        if (stageToComplete == 1 && body.position.z >= finishZ)
            stageManager.CompleteStageOne();
        else if (stageToComplete == 2 && body.position.x >= finishX)
            stageManager.CompleteStageTwo();
        else if (stageToComplete == 3 && body.position.z >= finishZ)
            stageManager.CompleteStageThree();
        else if (stageToComplete == 4 && body.position.z >= finishZ)
            stageManager.CompleteStageFour();
        else if (stageToComplete == 5 && body.position.z >= finishZ)
            stageManager.CompleteStageFive();
    }
}
