using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Save System/Saveable Object")]
public class SaveableObject : MonoBehaviour
{
    [SerializeField] private string saveId;
    [SerializeField] private bool saveActiveState = true;
    [SerializeField] private bool saveTransform = true;

    public string SaveId => EnsureSaveId();
    public bool SaveActiveState => saveActiveState;
    public bool SaveTransform => saveTransform;

    private void Awake()
    {
        EnsureSaveId();
    }

    private void Reset()
    {
        EnsureSaveId();
    }

    private void OnValidate()
    {
        EnsureSaveId();
    }

    public void RestoreState(bool active, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (saveTransform)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = scale;
        }

        if (saveActiveState)
        {
            gameObject.SetActive(active);
        }
    }

    private string EnsureSaveId()
    {
        if (string.IsNullOrWhiteSpace(saveId))
        {
            saveId = Guid.NewGuid().ToString("N");
        }

        return saveId;
    }
}
