using Microsoft.AspNetCore.SignalR;

namespace CARE.Hubs
{
    /// <summary>
    /// SignalR hub. Clients auto-join "battle" group on connect.
    /// Server sends:
    ///   "RankingUpdate"   → full ranked list
    ///   "ClassroomUpdate" → single updated classroom
    /// </summary>
    public class BattleHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "battle");
            await base.OnConnectedAsync();
        }
    }
}
