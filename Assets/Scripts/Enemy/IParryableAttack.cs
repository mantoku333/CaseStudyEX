namespace GameName.Enemy
{
    public interface IParryableAttack
    {
        bool IsParryable { get; }
        void StopByParry();
    }
}
