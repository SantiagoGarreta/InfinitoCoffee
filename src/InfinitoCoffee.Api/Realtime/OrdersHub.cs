using Microsoft.AspNetCore.SignalR;

namespace InfinitoCoffee.Api.Realtime;

public sealed class OrdersHub : Hub<IOrdersClient>;
