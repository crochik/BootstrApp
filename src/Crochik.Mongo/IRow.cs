namespace Crochik.Mongo
{
    public interface IRow<T>
    {
        T Id { get; }
    }
}