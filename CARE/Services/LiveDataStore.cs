using System.Collections.Concurrent;
using CARE.ViewModels;

namespace CARE.Services
{
    /// <summary>
    /// Thread-safe in-memory store for the latest reading per classroom.
    /// Avoids hitting the DB on every SignalR push / page refresh.
    /// </summary>
    public class LiveDataStore
    {
        private readonly ConcurrentDictionary<string, ClassroomLiveVm> _data = new();

        public void Upsert(ClassroomLiveVm vm)
        {
            _data[vm.Name.ToUpperInvariant()] = vm;
        }

        public ClassroomLiveVm? Get(string name) =>
            _data.TryGetValue(name.ToUpperInvariant(), out var vm) ? vm : null;

        public IReadOnlyList<ClassroomLiveVm> GetAllRanked() =>
            _data.Values
                 .OrderByDescending(v => v.TotalScore)
                 .Select((v, i) => v with { Position = i + 1 })
                 .ToList();

        public bool IsOnline(string name) =>
            _data.TryGetValue(name.ToUpperInvariant(), out var vm)
            && (DateTime.UtcNow - vm.LastSeen).TotalSeconds < 35;
    }
}
