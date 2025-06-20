namespace lib.common
{
    public interface Iterator<T>
    {
        bool HasNext();
        
        T Next();
    }
}