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
        if (!stageManager.IsGameStarted)
            return;

        Vector3 sectionOffset = StageManager.GetSectionOffset(stageToComplete);
        if (stageToComplete == 1 && body.position.z >= finishZ)
            stageManager.CompleteStageOne();
        else if (stageToComplete == 2 && body.position.x >= finishX + sectionOffset.x)
            stageManager.CompleteStageTwo();
        else if (stageToComplete == 3 && body.position.z >= finishZ + sectionOffset.z)
            stageManager.CompleteStageThree();
        else if (stageToComplete == 4 && body.position.z >= finishZ + sectionOffset.z)
            stageManager.CompleteStageFour();
        else if (stageToComplete == 5 && body.position.z >= finishZ + sectionOffset.z)
            stageManager.CompleteStageFive();
        else if (stageToComplete == 6 && body.position.z >= finishZ + sectionOffset.z)
            stageManager.CompleteStageSix();
        else if (stageToComplete == 7 && body.position.z >= finishZ)
            stageManager.CompleteStageSeven();
    }
}
