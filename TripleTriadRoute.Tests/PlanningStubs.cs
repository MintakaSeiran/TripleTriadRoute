using System.Numerics;

// Game services are replaced only at the boundary; planner, eligibility and session are real sources.
namespace AutoTripleTriadGrind
{
    public class Configuration
    {
        public HashSet<ushort> SelectedCards = [];
        public int MaxMatchFee;
    }
    public enum TriadRunMode { Collect, Farm }
}
namespace AutoTripleTriadGrind.Core
{
    public enum RunLogLevel { Error }
    public static class RunLog
    {
        public static void Record(RunLogLevel level, Exception exception, string text) { }
    }
}
namespace AutoTripleTriadGrind.Core.Triad.Match
{
    public enum MatchOutcome { Won, Lost, Drawn }
}
namespace AutoTripleTriadGrind.Core.Triad.Data
{
    public sealed class TestNpc
    {
        public uint TerritoryId = 1;
        public int Fee = 2;
        public bool Unlocked = true;
        public Vector3 Position;
        public byte Expansion;
    }
    public sealed class TestData
    {
        public TestNpc[] Npcs = [new(), new()];
        public int NpcCount => Npcs.Length;
        public ushort[] CardIdsInOrder = [1, 2, 3];
        public bool IsNpcReward(ushort id) => true;
        public ReadOnlySpan<ushort> NpcsDropping(ushort id) => id switch { 1 => new ushort[] { 0, 1 }, 2 => new ushort[] { 0 }, _ => new ushort[] { 1 } };
    }
    public static class TriadData
    {
        public const ushort NoNpc = ushort.MaxValue;
        public static TestData Set = new();
    }
    public static class TriadOwnership { public static bool IsOwned(ushort id) => false; }
    public static class TriadUnlock { public static bool IsUnlocked(int id) => TriadData.Set.Npcs[id].Unlocked; }
    public static class TriadDataLoader { public const uint BattleHallTerritoryId = 999; }
}
namespace ECommons.GameHelpers
{
    public static class Player
    {
        public static bool Available => false;
        public static (uint RowId, int _) Territory => (0, 0);
        public static Vector3 Position => Vector3.Zero;
    }
}
