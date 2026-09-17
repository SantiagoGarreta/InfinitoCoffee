using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Application.Orders.Dtos;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeOrderEventPublisher : IOrderEventPublisher
{
    private readonly Func<int>? _saveChangesCallsProvider;

    public FakeOrderEventPublisher(Func<int>? saveChangesCallsProvider = null)
    {
        _saveChangesCallsProvider = saveChangesCallsProvider;
    }

    public List<(string EventName, OrderRealtimeDto Order, CancellationToken CancellationToken, int SaveChangesCallsAtPublish)> Events { get; } = [];

    public Task OrderCreatedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        Events.Add(("OrderCreated", order, cancellationToken, _saveChangesCallsProvider?.Invoke() ?? 0));
        return Task.CompletedTask;
    }

    public Task OrderStatusChangedAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        Events.Add(("OrderStatusChanged", order, cancellationToken, _saveChangesCallsProvider?.Invoke() ?? 0));
        return Task.CompletedTask;
    }

    public Task OrderCancelledAsync(OrderRealtimeDto order, CancellationToken cancellationToken = default)
    {
        Events.Add(("OrderCancelled", order, cancellationToken, _saveChangesCallsProvider?.Invoke() ?? 0));
        return Task.CompletedTask;
    }
}
