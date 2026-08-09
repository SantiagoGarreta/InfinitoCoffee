namespace InfinitoCoffee.Application.Authentication.Exceptions;

public sealed class InvalidCredentialsException : Exception
{
    public const string DefaultMessage = "Invalid username or password.";

    public InvalidCredentialsException()
        : base(DefaultMessage)
    {
    }
}
