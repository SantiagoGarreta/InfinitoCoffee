namespace InfinitoCoffee.Domain.Common;

public static class UtcDateTime
{
    public static DateTime Ensure(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The value must be expressed in UTC.", parameterName);
        }

        return value;
    }
}
