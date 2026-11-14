using System.Runtime.CompilerServices;

namespace Caprine.CronTimer;

public sealed partial class CronExpression
{
    private sealed class ZoneMap
    {
        internal ReadOnlySpan<TimeSpan> Offsets => _offsets;

        private readonly TimeZoneInfo _zone;
        private readonly TimeSpan[]   _offsets;

        private static readonly ConditionalWeakTable<TimeZoneInfo, ZoneMap> Cache = new();

        private ZoneMap(TimeZoneInfo zone)
        {
            _zone = zone;
            var offsets = new HashSet<TimeSpan> { zone.BaseUtcOffset };
            foreach (var rule in zone.GetAdjustmentRules())
            {
                var standard = zone.BaseUtcOffset + rule.BaseUtcOffsetDelta;
                offsets.Add(standard);
                offsets.Add(standard + rule.DaylightDelta);
            }

            _offsets = [.. offsets.OrderDescending()];
        }

        internal bool TryMap(DateTime local, TimeSpan offset, out DateTimeOffset instant)
        {
            var utcTicks = local.Ticks - offset.Ticks;
            if (utcTicks < 0 || utcTicks > DateTime.MaxValue.Ticks)
            {
                instant = default;
                return false;
            }

            instant = new DateTimeOffset(local, offset);
            // Resolve from UTC, then check the round trip. Some .NET 10 Unix zones
            // report incorrect local ambiguity for negative daylight adjustments.
            // This also covers changes to the standard offset, not just DST.
            return _offsets.Length == 1 || _zone.GetUtcOffset(instant) == offset;
        }

        internal bool IsMissing(DateTime local)
        {
            foreach (var offset in _offsets)
            {
                if (TryMap(local, offset, out _))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool IsRepeated(DateTime local) => GetOffsetBounds(local, out _, out _);

        internal bool GetOffsetBounds(DateTime local, out TimeSpan earlierOffset, out TimeSpan laterOffset)
        {
            earlierOffset = TimeSpan.MinValue;
            laterOffset   = TimeSpan.MaxValue;
            if (_offsets.Length == 1)
            {
                return false;
            }

            foreach (var offset in _offsets)
            {
                if (TryMap(local, offset, out _))
                {
                    if (offset > earlierOffset)
                    {
                        earlierOffset = offset;
                    }

                    if (offset < laterOffset)
                    {
                        laterOffset = offset;
                    }
                }
            }

            return earlierOffset > laterOffset;
        }

        internal static ZoneMap For(TimeZoneInfo zone) => Cache.GetValue(zone, static value => new ZoneMap(value));
    }
}
