using InfinitoCoffee.Application.Common.Time;

namespace InfinitoCoffee.Infrastructure.Time;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
