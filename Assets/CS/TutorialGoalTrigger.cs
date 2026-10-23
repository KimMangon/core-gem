using Unity.Netcode;
using UnityEngine;

public class TutorialGoalTrigger : MonoBehaviour
{
    private StageManager manager;
    private void OnTriggerEnter(Collider other) => Check(other);
    private void OnTriggerStay(Collider other) => Check(other);

    private void Check(Collider other)
    {
        if (manager == null)
            manager = StageManager.Instance;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer ||
            manager == null || !manager.IsTutorialStage)
            return;
        Rigidbody body = other.attachedRigidbody;
        TutorialCombatLesson lesson = FindFirstObjectByType<TutorialCombatLesson>();
        if (lesson != null && !lesson.CanFinish) return;
        if (body != null && body.GetComponent<BoulderSlopeDrag>() != null)
            manager.CompleteTutorial();
    }
}
