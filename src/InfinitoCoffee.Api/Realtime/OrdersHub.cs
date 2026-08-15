using InfinitoCoffee.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InfinitoCoffee.Api.Realtime;

[Authorize(Policy = AuthorizationPolicyNames.AllOperationalRoles)]
public sealed class OrdersHub : Hub<IOrdersClient>;
