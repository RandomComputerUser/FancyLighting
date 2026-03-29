using System.Diagnostics;

namespace FancyLighting.Utils;

internal static class PerformanceTracker
{
    private record StatTracker
    {
        public long StartTime { get; set; }

        public int SampleSize { get; private set; }

        private double Sum { get; set; }

        private double SumSquares { get; set; }

        public void Add(double value)
        {
            ++SampleSize;
            Sum += value;
            SumSquares += value * value;
        }

        public double Mean() => Sum / SampleSize;

        public double StandardDeviation()
        {
            var ex = Sum / SampleSize;
            var ex2 = SumSquares / SampleSize;
            var variance = ((double)SampleSize / (SampleSize - 1)) * (ex2 - (ex * ex));
            return Math.Sqrt(variance);
        }

        public void Reset()
        {
            SampleSize = 0;
            Sum = 0.0;
            SumSquares = 0.0;
        }
    }

    public static bool Enabled
    {
        get;
        internal set
        {
            field = value;
            if (!value)
            {
                _stats.Clear();
            }
        }
    }

    public const int MaxSampleSize = 50;

    private static Dictionary<string, StatTracker> _stats = new();

    public static void StartTiming(string label)
    {
        if (!Enabled)
        {
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        if (!_stats.TryGetValue(label, out var stats))
        {
            _stats[label] = stats = new();
        }

        stats.StartTime = timestamp;
    }

    public static void StopTiming(string label)
    {
        if (!Enabled)
        {
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        if (!_stats.TryGetValue(label, out var stats))
        {
            return;
        }

        var elapsedTime = Stopwatch.GetElapsedTime(stats.StartTime, timestamp);
        stats.Add(elapsedTime.TotalNanoseconds);

        if (stats.SampleSize < MaxSampleSize)
        {
            return;
        }

        var mean = stats.Mean();
        var standardDeviation = stats.StandardDeviation();
        stats.Reset();

        Main.NewText(
            $"{label}: {mean / 1000.0:F1} ± {1.96 * standardDeviation / 1000.0:F1} μs"
        );
    }
}
