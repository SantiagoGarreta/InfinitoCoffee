namespace InfinitoCoffee.Application.Common.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string resourceName, object key)
        : base($"The resource '{resourceName}' with key '{key}' was not found.")
    {
        ResourceName = resourceName;
        Key = key;
    }

    public string ResourceName { get; }

    public object Key { get; }
}
