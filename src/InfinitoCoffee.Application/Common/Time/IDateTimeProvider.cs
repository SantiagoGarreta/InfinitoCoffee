namespace InfinitoCoffee.Application.Common.Time;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
