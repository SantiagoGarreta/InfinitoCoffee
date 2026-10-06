using InfinitoCoffee.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InfinitoCoffee.Api.Realtime;

[Authorize(Policy = AuthorizationPolicyNames.AllOperationalRoles)]
public sealed class OrdersHub(InfinitoCoffee.Application.Branches.IBranchContext branch) : Hub<IOrdersClient>
{
    public override async Task OnConnectedAsync()
    {
        var branchId = Context.GetHttpContext()?.Items["BranchId"] as int? ?? branch.BranchId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}");
        await base.OnConnectedAsync();
    }
}
