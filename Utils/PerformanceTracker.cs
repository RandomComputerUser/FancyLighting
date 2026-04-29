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

        public double Min { get; private set; } = double.PositiveInfinity;

        public double Max { get; private set; } = double.NegativeInfinity;

        public void Add(double value)
        {
            ++SampleSize;
            Sum += value;
            SumSquares += value * value;
            Min = Math.Min(value, Min);
            Max = Math.Max(value, Max);
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
            Min = double.PositiveInfinity;
            Max = double.NegativeInfinity;
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

    private static readonly Dictionary<string, StatTracker> _stats = new();

    private static long _lastDisplayTime = 0;

    public const double DisplayIntervalSeconds = 10.0;

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
    }

    public static void DisplayStatistics(bool force)
    {
        if (!Enabled)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        if (!force)
        {
            var elapsedSeconds = Stopwatch
                .GetElapsedTime(_lastDisplayTime, now)
                .TotalSeconds;

            if (elapsedSeconds < DisplayIntervalSeconds)
            {
                return;
            }
        }
        _lastDisplayTime = now;

        foreach (var (label, stats) in _stats.OrderBy(entry => entry.Key))
        {
            if (stats.SampleSize <= 0)
            {
                continue;
            }

            var mean = stats.Mean();
            var standardDeviation = stats.StandardDeviation();
            var min = stats.Min;
            var max = stats.Max;
            stats.Reset();

            Main.NewText(
                $"{label}: {mean / 1000.0:F1} ± {1.96 * standardDeviation / 1000.0:F1} μs (Min: {min / 1000.0:F1} μs, Max: {max / 1000.0:F1} μs)"
            );
        }
    }
}
