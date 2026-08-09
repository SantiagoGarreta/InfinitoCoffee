using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InfinitoCoffee.Api.Realtime;

[AllowAnonymous]
public sealed class PickupHub : Hub<IPickupClient>;
