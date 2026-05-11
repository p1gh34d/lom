using UnityEngine;

public class NoteController : MonoBehaviour
{
    public int Midi { get; set; }
    public bool IsForced { get; set; }
    public float Duration { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSustained { get; set; } = false;
    public bool IsOpen { get; set; }
    public bool IsStarPower { get; set; } // Является ли нота Star Power
    public Transform StickTransform { get; private set; }
    public Transform BaseTransform { get; private set; }
    public float TargetTime { get; private set; }

    private NoteSpawner noteSpawner;
    private NoteInputManager noteInputManager;

public void Initialize(NoteSpawner spawner, NoteInputManager inputManager, float targetTime, bool isStarPower = false)
{
    noteSpawner = spawner;
    noteInputManager = inputManager;
    TargetTime = targetTime;
    IsStarPower = isStarPower; // Устанавливаем Star Power
    foreach (Transform child in transform)
    {
        if (child != null && child.name.StartsWith("NoteStick"))
        {
            StickTransform = child;
            break;
        }
    }
}

    public void SetBaseTransform(Transform baseTransform)
    {
        BaseTransform = baseTransform;
    }

public void Hit()
{
    if (Duration <= 0.1f)
    {
        Destroy(gameObject);
    }
    else
    {
        HideHeadRecursive(transform);
        IsActive = false;
        IsSustained = true;
    }
}

    public void Miss()
    {
        Destroy(gameObject);
    }

    private void HideHeadRecursive(Transform current)
    {
        if (current == null) return;
        if (current.name.StartsWith("NoteStick")) return; // не скрываем палочку

        MeshRenderer renderer = current.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            HideHeadRecursive(current.GetChild(i));
        }
    }
}