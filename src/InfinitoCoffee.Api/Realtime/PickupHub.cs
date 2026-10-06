using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InfinitoCoffee.Api.Realtime;

[AllowAnonymous]
public sealed class PickupHub(InfinitoCoffee.Application.Branches.IBranchContext branch) : Hub<IPickupClient>
{
    public override async Task OnConnectedAsync()
    {
        var branchId = Context.GetHttpContext()?.Items["BranchId"] as int? ?? branch.BranchId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}");
        await base.OnConnectedAsync();
    }
}
