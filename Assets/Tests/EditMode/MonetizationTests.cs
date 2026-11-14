using System;
using System.Linq;
using BlockPuzzle.Core;
using BlockPuzzle.Core.Monetization;
using NUnit.Framework;

namespace BlockPuzzle.Tests
{
    public class DirectorTests
    {
        static RunContext Ctx(ValueTier tier, float minutes, bool adReady = true, int round = 5) => new RunContext
        {
            Round = round, RunMinutes = minutes, Score = 100, BestScore = 5000,
            RewardedAdReady = adReady, RevivesLeft = 1, Tier = tier, SqueezeMinute = 10f,
        };

        [Test]
        public void OpeningTraysAreAssisted()
        {
            var d = new DifficultyDirector(new MonetizationConfig());
            Assert.AreEqual(DealMode.Assist, d.Decide(Ctx(ValueTier.High, 0f, round: 0), out _));
        }

        [Test]
        public void HighValuePlayerIsSqueezedOnlyWhenAdReadyAndMinuteReached()
        {
            var d = new DifficultyDirector(new MonetizationConfig());
            Assert.AreEqual(DealMode.Normal, d.Decide(Ctx(ValueTier.High, 5f), out _));
            Assert.AreEqual(DealMode.Squeeze, d.Decide(Ctx(ValueTier.High, 11f), out _));
            Assert.AreEqual(DealMode.Normal, d.Decide(Ctx(ValueTier.High, 11f, adReady: false), out _));
        }

        [Test]
        public void LowValuePlayerIsNeverSqueezed()
        {
            var d = new DifficultyDirector(new MonetizationConfig());
            Assert.AreEqual(DealMode.Normal, d.Decide(Ctx(ValueTier.Low, 60f), out _));
        }

        [Test]
        public void NearBestTriggersEarlySqueeze()
        {
            var d = new DifficultyDirector(new MonetizationConfig());
            var ctx = Ctx(ValueTier.High, 3f);
            ctx.Score = 4600;
            Assert.AreEqual(DealMode.Squeeze, d.Decide(ctx, out _));
        }

        [Test]
        public void DisabledSwitchForcesNormal()
        {
            var d = new DifficultyDirector(new MonetizationConfig { dynamicDifficultyEnabled = false });
            Assert.AreEqual(DealMode.Normal, d.Decide(Ctx(ValueTier.High, 60f, round: 0), out _));
        }
    }

    public class ValueTests
    {
        [Test]
        public void ConsentDeclinedUsesConfiguredTier()
        {
            var c = new MonetizationConfig();
            var p = new PlayerProfile { consentAsked = true, consentGiven = false, recentAdRevenue = 5 };
            Assert.AreEqual(ValueTier.Low, ValueClassifier.Classify(p, new LinearAdValuePredictor(c), c, out _));
        }

        [Test]
        public void AdHistoryRaisesValueAndHarvestAppliesAfterThreeViews()
        {
            var c = new MonetizationConfig();
            var predictor = new LinearAdValuePredictor(c);
            var fresh = new PlayerProfile { consentGiven = true };
            var watcher = new PlayerProfile { consentGiven = true, country = "US", deviceModel = "iPhone16,1" };
            for (int i = 0; i < 3; i++) watcher.RecordAdRevenue(0.02, true);
            watcher.CloseSession(0.06);

            ValueClassifier.Classify(fresh, predictor, c, out float v0);
            var tier = ValueClassifier.Classify(watcher, predictor, c, out float v1);
            Assert.Greater(v1, v0);
            Assert.AreEqual(ValueTier.High, tier);
        }
    }

    public class BanditTests
    {
        [Test]
        public void ExploresEveryArmThenExploitsBest()
        {
            var b = new EpsilonGreedyBandit(3, null, new Random(1));
            for (int i = 0; i < 3; i++) { int arm = b.Select(0f); b.Update(arm, arm == 1 ? 1.0 : 0.1); }
            Assert.AreEqual(1, b.Select(0f));
        }
    }

    public class DealModeTests
    {
        static Board HalfFull()
        {
            var b = new Board();
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if ((x + y) % 3 != 0 && y < 6) b.Set(x, y, 1);
            return b;
        }

        [Test]
        public void AssistDealsPiecesThatAllFit()
        {
            var board = HalfFull();
            for (int seed = 0; seed < 30; seed++)
                Assert.IsTrue(new PieceGenerator(seed).NextTray(board, 0, DealMode.Assist).All(board.CanPlaceAnywhere));
        }

        [Test]
        public void SqueezeIsHarderThanNormal()
        {
            var board = HalfFull();
            int normal = 0, squeeze = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                normal += new PieceGenerator(seed).NextTray(board, 0).Sum(board.CountPlacements);
                squeeze += new PieceGenerator(seed).NextTray(board, 0, DealMode.Squeeze).Sum(board.CountPlacements);
            }
            Assert.Less(squeeze, normal);
        }

        [Test]
        public void ReviveClearKeepsOnlyUntouchedCells()
        {
            var board = new Board();
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) board.Set(x, y, 1);
            var result = board.ClearLines(board.FullestLines(true, 2), board.FullestLines(false, 2));
            Assert.AreEqual(64 - 36, result.ClearedCells.Count);
            Assert.AreEqual(36, board.FilledCount);
        }
    }
}
