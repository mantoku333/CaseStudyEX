using UnityEngine;

public interface ISaveDataModule
{
    int Priority { get; }
    void Capture(SaveGameData saveData);
    void Restore(SaveGameData saveData);
}

public abstract class SaveDataModuleBehaviour : MonoBehaviour, ISaveDataModule
{
    [SerializeField]
    private int priority;

    public virtual int Priority => priority;

    protected virtual void OnEnable()
    {
        SaveManager.RegisterModule(this);
    }

    protected virtual void OnDisable()
    {
        SaveManager.UnregisterModule(this);
    }

    public abstract void Capture(SaveGameData saveData);
    public abstract void Restore(SaveGameData saveData);
}
