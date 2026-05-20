using Microsoft.AspNetCore.SignalR;
using CARE.Hubs;
using CARE.ViewModels;

namespace CARE.Services
{
    /// <summary>
    /// Wraps SignalR hub context. Inject this instead of IHubContext directly.
    /// Avoids IClientProxy.SendAsync extension method resolution issues.
    /// </summary>
    public class BattleNotifier
    {
        private readonly IHubContext<BattleHub> _hub;

        public BattleNotifier(IHubContext<BattleHub> hub)
        {
            _hub = hub;
        }

        public Task PushRanking(IReadOnlyList<ClassroomLiveVm> ranked)
            => _hub.Clients.Group("battle").SendAsync("RankingUpdate", ranked);

        public Task PushClassroom(ClassroomLiveVm vm)
            => _hub.Clients.Group("battle").SendAsync("ClassroomUpdate", vm);

        public Task PushBoth(ClassroomLiveVm vm, IReadOnlyList<ClassroomLiveVm> ranked)
        {
            var t1 = PushRanking(ranked);
            var t2 = PushClassroom(vm);
            return Task.WhenAll(t1, t2);
        }
    }
}
