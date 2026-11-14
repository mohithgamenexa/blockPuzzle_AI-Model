using System;

namespace BlockPuzzle.Core.Monetization
{
    [Serializable]
    public sealed class BanditState
    {
        public int[] pulls = new int[0];
        public double[] rewardSums = new double[0];
    }

    /// <summary>
    /// Multi-armed bandit used to A/B test the squeeze minute. Each arm is one candidate
    /// minute; the reward is ad revenue earned in the run. Mostly exploits the best arm,
    /// explores with probability epsilon, and tries every arm once first.
    /// </summary>
    public sealed class EpsilonGreedyBandit
    {
        readonly Random _rng;
        public readonly BanditState State;
        public int ArmCount { get; }

        public EpsilonGreedyBandit(int armCount, BanditState state, Random rng)
        {
            ArmCount = armCount;
            _rng = rng;
            State = state ?? new BanditState();
            if (State.pulls == null || State.pulls.Length != armCount)
            {
                State.pulls = new int[armCount];
                State.rewardSums = new double[armCount];
            }
        }

        public int Select(float epsilon)
        {
            for (int i = 0; i < ArmCount; i++) if (State.pulls[i] == 0) return i;
            if (_rng.NextDouble() < epsilon) return _rng.Next(ArmCount);

            int best = 0;
            for (int i = 1; i < ArmCount; i++) if (Mean(i) > Mean(best)) best = i;
            return best;
        }

        public void Update(int arm, double reward)
        {
            if (arm < 0 || arm >= ArmCount) return;
            State.pulls[arm]++;
            State.rewardSums[arm] += reward;
        }

        public double Mean(int arm) => State.pulls[arm] == 0 ? 0 : State.rewardSums[arm] / State.pulls[arm];
    }
}
