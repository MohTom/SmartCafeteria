namespace Cafeteria.RemoteAccess;

public sealed class RemoteAccessException : Exception
{
    public RemoteAccessException(string message) : base(message)
    {
    }

    public RemoteAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
