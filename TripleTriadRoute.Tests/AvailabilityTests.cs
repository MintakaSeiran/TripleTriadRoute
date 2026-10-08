using System.Linq;
using AutoTripleTriadGrind.Core.Game.Ops;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Tasks;
using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Tests;

public class AvailabilityTests
{
    private static TriadMenuSnapshot Menu(params string[] entries) => new("SelectString", entries, new uint[entries.Length]);

    [Fact]
    public void TwoCompletedTalkOnlyInteractionsSkipNpc()
    {
        var check = new TriadAvailabilityCheck();
        Assert.Equal(SkipReason.None, check.ObserveTalkOnly());
        Assert.Equal(SkipReason.TriadUnavailable, check.ObserveTalkOnly());
        Assert.Equal(2, check.Attempts);
    }

    [Fact]
    public void TalkAfterSelectedChallengeIsInteractionFailure()
        => Assert.Equal(SkipReason.InteractFailed, new TriadAvailabilityCheck { SelectedTriad = true }.ObserveTalkOnly());

    [Fact]
    public void MixedMenuAndTalkEvidenceIsNotUnavailable()
    {
        var check = new TriadAvailabilityCheck();
        check.Observe(Menu("話す", "キャンセル"));
        Assert.Equal(SkipReason.MenuRecognitionFailed, check.ObserveTalkOnly());
        check = new TriadAvailabilityCheck();
        check.ObserveTalkOnly();
        Assert.Equal(SkipReason.MenuRecognitionFailed, check.Observe(Menu("話す", "キャンセル")));
    }

    [Theory]
    [InlineData("カード対戦を申し込む")]
    [InlineData("カード対戦")]
    [InlineData("Triple Triad")]
    [InlineData("TRIPLE TRIAD")]
    [InlineData("Triple-Triad")]
    [InlineData("triade")]
    [InlineData("triplo")]
    [InlineData("トリプルトライアド")]
    [InlineData("幻卡")]
    public void LocalizedChallenge(string text)
        => Assert.Equal(1, TriadMenuPolicy.Find(["話す", text, "キャンセル"], [0, 0, 0]));

    [Theory]
    [InlineData(60091u)]
    [InlineData(61721u)]
    [InlineData(61723u)]
    public void IconHasPriority(uint icon)
        => Assert.Equal(1, TriadMenuPolicy.Find(["Triple Triad", "localized entry"], [0, icon]));

    [Theory]
    [InlineData("ショップ")]
    [InlineData("キャンセル")]
    [InlineData("Accept quest")]
    [InlineData("")]
    public void SingleUnknownEntryIsNeverClicked(string text)
        => Assert.Equal(-1, TriadMenuPolicy.Find([text], [0]));

    [Theory]
    [InlineData("話す", "キャンセル")]
    [InlineData("ショップ", "キャンセル")]
    [InlineData("Talk", "Cancel")]
    public void TwoIndependentIdenticalMenusRequired(string text, string cancel)
    {
        var check = new TriadAvailabilityCheck();
        Assert.Equal(SkipReason.None, check.Observe(Menu(text, cancel)));
        Assert.Equal(SkipReason.TriadUnavailable, check.Observe(Menu(text, cancel)));
        Assert.Equal(2, check.Attempts);
    }

    [Fact]
    public void ShopTalkCancelIsUnavailableOnlyAfterRecheck()
    {
        var check = new TriadAvailabilityCheck();
        Assert.Equal(SkipReason.None, check.Observe(Menu("ショップ", "話す", "キャンセル")));
        Assert.Equal(SkipReason.TriadUnavailable, check.Observe(Menu("ショップ", "話す", "キャンセル")));
    }

    [Fact]
    public void ChangedMenuIsNotUnavailable()
    {
        var check = new TriadAvailabilityCheck();
        check.Observe(Menu("話す", "キャンセル"));
        Assert.Equal(SkipReason.MenuRecognitionFailed, check.Observe(Menu("ショップ", "キャンセル")));
    }

    [Theory]
    [InlineData("カード対戦を申し込む")]
    [InlineData("Triple Triad")]
    [InlineData("未知の翻訳")]
    [InlineData("クエストを受注する")]
    [InlineData("テレポ")]
    public void UnrecognizedChallengeOrUnknownTextNeverMeansUnavailable(string text)
        => Assert.Equal(SkipReason.MenuRecognitionFailed, new TriadAvailabilityCheck().Observe(Menu(text, "キャンセル")));

    [Fact]
    public void IconEvidenceCannotBeUnavailable()
        => Assert.Equal(SkipReason.MenuRecognitionFailed, new TriadAvailabilityCheck().Observe(new("SelectIconString", ["話す", "キャンセル"], [60091, 0])));

    [Fact]
    public void UnreadableMenuCannotBeUnavailable()
    {
        Assert.Equal(SkipReason.MenuRecognitionFailed, new TriadAvailabilityCheck().Observe(null));
        Assert.Equal(SkipReason.MenuRecognitionFailed, new TriadAvailabilityCheck().Observe(Menu()));
    }

    [Fact]
    public void SelectedChallengeWithoutWindowRetainsInteractionFailure()
        => Assert.Equal(SkipReason.InteractFailed, new TriadAvailabilityCheck { SelectedTriad = true }.Observe(Menu("話す", "キャンセル")));

    [Theory]
    [InlineData("キャンセル", true)]
    [InlineData(" Cancel ", true)]
    [InlineData("Annuler", true)]
    [InlineData("Abbrechen", true)]
    [InlineData("取消", true)]
    [InlineData("キャンセルについて話す", false)]
    [InlineData("Shop", false)]
    public void OnlyExactCancelMayBeClicked(string text, bool expected)
        => Assert.Equal(expected, TriadMenuPolicy.IsCancel(text));

    [Fact]
    public void ExcludedNpcCardIsReassignedAndSoleSourceDoesNotStopOtherCards()
    {
        var configuration = new Configuration();
        configuration.SelectedCards.UnionWith([1, 2, 3]);
        TriadData.Set = new TestData();
        var excluded = new Dictionary<int, SkipReason> { [0] = SkipReason.TriadUnavailable };
        var plan = CollectPlanner.Build(configuration, excluded);
        Assert.Single(plan.Assignments);
        Assert.Equal((ushort)1, plan.Assignments[0].NpcIndex);
        Assert.Equal(new ushort[] { 1, 3 }, plan.Assignments[0].Cards);
        Assert.Equal(new UnavailableCard(2, SkipReason.TriadUnavailable), Assert.Single(plan.Unavailable));
        Assert.Equal(3, plan.WantedCards);
        Assert.Contains(CollectPlanner.Build(configuration).Assignments, assignment => assignment.NpcIndex == 0);
    }

    [Fact]
    public void StaticEligibilityRemainsSeparate()
    {
        TriadData.Set = new TestData();
        var configuration = new Configuration();
        TriadData.Set.Npcs[0].Unlocked = false;
        Assert.Equal(SkipReason.Locked, NpcEligibility.Check(0, configuration));
        TriadData.Set.Npcs[0].Unlocked = true;
        configuration.MaxMatchFee = 1;
        Assert.Equal(SkipReason.FeeTooHigh, NpcEligibility.Check(0, configuration));
        TriadData.Set.Npcs[0].TerritoryId = TriadDataLoader.BattleHallTerritoryId;
        Assert.Equal(SkipReason.BattleHall, NpcEligibility.Check(0, configuration));
    }

    [Fact]
    public void ExclusionsSurviveTaskRestartButNotNewSession()
    {
        var session = new AutoTriadSession(TriadRunMode.Collect);
        session.RecordSkip(0, SkipReason.TriadUnavailable);
        session.RecordSkip(0, SkipReason.InteractFailed);
        Assert.Equal(SkipReason.TriadUnavailable, session.ExcludedNpcs[0]);
        Assert.Single(session.SkippedNpcs);
        Assert.Empty(new AutoTriadSession(TriadRunMode.Collect).ExcludedNpcs);
        Assert.Empty(new AutoTriadSession(TriadRunMode.Farm).ExcludedNpcs);
    }
}
