using InfinitoCoffee.Application.Common.Time;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; }
}
