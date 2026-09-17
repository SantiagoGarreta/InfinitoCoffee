namespace InfinitoCoffee.Domain.Orders.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
