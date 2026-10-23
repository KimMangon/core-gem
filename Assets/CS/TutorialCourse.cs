using UnityEngine;

/// <summary>Editable markers for the additively loaded tutorial scene.</summary>
public class TutorialCourse : MonoBehaviour
{
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private Transform boulderSpawn;
    [SerializeField] private Transform goal;

    public Vector3 PlayerSpawn => playerSpawn != null ? playerSpawn.position : transform.position;
    public Vector3 BoulderSpawn => boulderSpawn != null ? boulderSpawn.position : transform.position;
    public Vector3 GoalPosition => goal != null ? goal.position : transform.position;
}
