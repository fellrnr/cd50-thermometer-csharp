using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureMonitor
{
    internal class TemperatureDeltaCalculator
    {
        private readonly object _sync = new();
        private readonly List<(DateTimeOffset Time, double TempC)> _readings = new();
        /// <summary>How long to retain readings. Default 10 minutes.</summary>
        public TimeSpan Retention { get; }

        public TemperatureDeltaCalculator(TimeSpan? retention = null)
        {
            Retention = retention ?? TimeSpan.FromMinutes(10);
        }

        public void AddReading(double tempC)
        {

            lock (_sync)
            {
                DateTimeOffset time = DateTimeOffset.Now;
                // keep list ordered by time ascending
                if (_readings.Count == 0 || time >= _readings[^1].Time)
                {
                    _readings.Add((time, tempC));
                }
                else
                {
                    var idx = _readings.BinarySearch((time, tempC), Comparer<(DateTimeOffset, double)>.Create((a, b) => a.Item1.CompareTo(b.Item1)));
                    if (idx < 0) idx = ~idx;
                    _readings.Insert(idx, (time, tempC));
                }

                TrimOld(readingsNow: time);
            }
        }

        //public void AddReading(SensorReading reading) =>
        //    AddReading(reading.TemperatureC, reading.Time);

        public double? GetDelta(TimeSpan period, DateTimeOffset? now = null)
        {
            now ??= DateTimeOffset.UtcNow;
            var target = now.Value - period;

            lock (_sync)
            {
                if (_readings.Count == 0) return null;

                // remove old entries relative to now
                TrimOld(readingsNow: now.Value);

                // need at least one point at or before now and one point at or before/after target
                var latest = _readings[^1];
                if (latest.Time > now) // if latest is in future, use interpolation later; otherwise use latest as current
                {
                    // allowed but rare; still handle
                }

                // compute temperature at now (interpolated/exact using latest two points)
                var tempNow = InterpolateAt(now.Value);
                if (tempNow is null) return null;

                // compute temperature at target time
                var tempThen = InterpolateAt(target);
                if (tempThen is null) return null;

                return tempNow.Value - tempThen.Value;
            }
        }

        public double? GetDeltaSeconds(int seconds, DateTimeOffset? now = null) =>
            GetDelta(TimeSpan.FromSeconds(seconds), now);

        private double? InterpolateAt(DateTimeOffset t)
        {
            if (_readings.Count == 0) return null;

            // if t is earlier than first or later than last, only exact match or extrapolate not allowed:
            var first = _readings[0];
            var last = _readings[^1];

            if (t <= first.Time) return first.TempC;
            if (t >= last.Time) return last.TempC;

            // find surrounding points
            int idx = _readings.BinarySearch((t, 0.0), Comparer<(DateTimeOffset, double)>.Create((a, b) => a.Item1.CompareTo(b.Item1)));
            if (idx >= 0)
            {
                return _readings[idx].TempC;
            }

            idx = ~idx;
            // idx is the index of the first item with Time > t, so idx-1 is <= t
            var after = _readings[idx];
            var before = _readings[idx - 1];

            var span = (after.Time - before.Time).TotalSeconds;
            if (span <= 0) return before.TempC;

            var ratio = (t - before.Time).TotalSeconds / span;
            return before.TempC + (after.TempC - before.TempC) * ratio;
        }

        private void TrimOld(DateTimeOffset readingsNow)
        {
            var cutoff = readingsNow - Retention;
            if (_readings.Count == 0) return;
            int removeCount = 0;
            while (removeCount < _readings.Count && _readings[removeCount].Time < cutoff) removeCount++;
            if (removeCount > 0) _readings.RemoveRange(0, removeCount);
        }
    }
}