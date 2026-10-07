using AutoTripleTriadGrind.Core.Planning;

namespace AutoTripleTriadGrind.Core.Game.Ops;

// Pure policy: an unknown translation is never evidence that the NPC cannot play.
internal static class TriadMenuPolicy
{
    internal static readonly uint[] Icons = [60091u, 61721u, 61723u];
    private static readonly string[] Words = ["Triple Triad", "Triple-Triad", "triad", "triade", "triplo", "トリプル", "カード対戦", "幻卡"];
    private static readonly string[] CancelWords = ["キャンセル", "Cancel", "Quit", "Abbrechen", "Annuler", "取消"];
    private static readonly string[] OtherWords = ["話す", "ショップ", "Talk", "Shop", "Small talk", "Purchase items", "Parler", "Acheter", "Reden", "Plaudern", "Kaufen", "交谈", "商店"];

    public static string? Keyword(string text) => Words.FirstOrDefault(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    public static bool IsCancel(string text) => CancelWords.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase);
    public static int Find(string[] entries, uint[] icons)
    {
        for (var i = 0; i < Math.Min(entries.Length, icons.Length); i++)
            if (Array.IndexOf(Icons, icons[i]) >= 0) return i;
        return Array.FindIndex(entries, text => Keyword(text) is not null);
    }

    public static bool ClearlyUnavailable(string[] entries)
        => entries.Length > 0 && entries.All(text => IsCancel(text) || OtherWords.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase));
}

internal sealed record TriadMenuSnapshot(string Addon, string[] Entries, uint[] Icons)
{
    public string Fingerprint => Addon + "\n" + string.Join("\n", Entries.Select((text, i) => $"{i}:{Icons[i]}:{text}"));
    public bool HasTriadEvidence => Entries.Any(text => TriadMenuPolicy.Keyword(text) is not null) || Icons.Any(icon => Array.IndexOf(TriadMenuPolicy.Icons, icon) >= 0);
}

internal sealed class TriadAvailabilityCheck
{
    private string? firstMenu;
    public int Attempts { get; private set; }
    public bool SelectedTriad { get; set; }

    // Called once per independently opened menu, never once per frame.
    public SkipReason Observe(TriadMenuSnapshot? menu)
    {
        Attempts++;
        if (menu is null || menu.HasTriadEvidence || !TriadMenuPolicy.ClearlyUnavailable(menu.Entries))
            return SkipReason.MenuRecognitionFailed;
        if (SelectedTriad) return SkipReason.InteractFailed;
        if (Attempts == 1)
        {
            firstMenu = menu.Fingerprint;
            return SkipReason.None;
        }
        return menu.Fingerprint == firstMenu ? SkipReason.TriadUnavailable : SkipReason.MenuRecognitionFailed;
    }
}
