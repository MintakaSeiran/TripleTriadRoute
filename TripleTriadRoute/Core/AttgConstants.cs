namespace AutoTripleTriadGrind.Core;

internal static class AttgConstants
{
    public const string PrimaryCommand = "/ttroute";
    public const string AliasCommand = "/triadroute";

    public const string LogPrefix = "[TripleTriadRoute]";

    public const int SaveThrottleMs = 500;

    internal static class ThrottleKeys
    {
        public const string Save = "TripleTriadRoute.Save";
    }
}
