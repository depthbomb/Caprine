using System.Numerics;

namespace Caprine.CronTimer;

internal sealed partial class CronExpressionData
{
    // Version 1: version, day flags, six field bitsets, four modifier bitsets,
    // and an optional 10,000-bit year set. Bits are ordered low to high in each byte.
    internal byte[] Encode()
    {
        var bytes = new List<byte>(42 + (Years is null ? 0 : 1250))
        {
            1,
            (byte)((DayOfMonthUnrestricted ? 1 : 0) |
                   (DayOfWeekUnrestricted ? 2 : 0)  |
                   (LastWeekday ? 4 : 0)            |
                   (Years is not null ? 8 : 0)),
        };
        WriteBits(bytes, Seconds);
        WriteBits(bytes, Minutes);
        WriteBits(bytes, Hours);
        WriteBits(bytes, Months);
        WriteBits(bytes, DaysOfMonth);
        WriteBits(bytes, DaysOfWeek);
        WriteSet(bytes, LastDayOffsets, 31);
        WriteSet(bytes, NearestWeekdays, 32);
        WriteSet(bytes, LastWeekdays, 7);
        WriteSet(bytes, NthWeekdays.Select(value => value.Day * 5 + value.Ordinal - 1), 35);

        if (Years is not null)
        {
            WriteBits(bytes, Years);
        }

        return [.. bytes];
    }

    private static void WriteBits(List<byte> bytes, bool[] values)
    {
        for (var start = 0; start < values.Length; start += 8)
        {
            byte value = 0;
            for (var bit = 0; bit < 8 && start + bit < values.Length; bit++)
            {
                if (values[start + bit])
                {
                    value |= (byte)(1 << bit);
                }
            }

            bytes.Add(value);
        }
    }

    private static void WriteSet(List<byte> bytes, IEnumerable<int> values, int size)
    {
        var bits = new bool[size];
        foreach (var value in values)
        {
            bits[value] = true;
        }

        WriteBits(bytes, bits);
    }

    #if !CRON_GENERATOR
    internal static CronExpressionData Decode(string expression, CronExpressionOptions options, ReadOnlySpan<byte> bytes)
    {
        var result = new CronExpressionData(expression, options, false);
        if (bytes.Length < 2 || bytes[0] != 1 || bytes[1] > 15 ||
            bytes.Length != 42 + ((bytes[1] & 8) != 0 ? 1250 : 0))
        {
            throw InvalidData();
        }

        result.DayOfMonthUnrestricted = (bytes[1] & 1) != 0;
        result.DayOfWeekUnrestricted  = (bytes[1] & 2) != 0;
        result.LastWeekday            = (bytes[1] & 4) != 0;

        var index = 2;
        ReadBits(bytes, ref index, result.Seconds);
        ReadBits(bytes, ref index, result.Minutes);
        ReadBits(bytes, ref index, result.Hours);
        ReadBits(bytes, ref index, result.Months);
        ReadBits(bytes, ref index, result.DaysOfMonth);
        ReadBits(bytes, ref index, result.DaysOfWeek);
        ReadSet(bytes, ref index, result.LastDayOffsets, 31);
        ReadSet(bytes, ref index, result.NearestWeekdays, 32);
        ReadSet(bytes, ref index, result.LastWeekdays, 7);

        var nth = ReadMask(bytes, ref index, 35);
        while (nth != 0)
        {
            var value = BitOperations.TrailingZeroCount(nth);
            result.NthWeekdays.Add((value / 5, value % 5 + 1));
            nth &= nth - 1;
        }

        if ((bytes[1] & 8) != 0)
        {
            if ((options & CronExpressionOptions.IncludeYear) == 0)
            {
                throw InvalidData();
            }

            result.Years = new bool[10000];
            ReadBits(bytes, ref index, result.Years);

            if (result.Years[0] || !result.Years.Contains(true))
            {
                throw InvalidData();
            }
        }

        if (result.Months[0]                   ||
            result.DaysOfMonth[0]              ||
            result.NearestWeekdays.Contains(0) ||
            !result.Seconds.Contains(true)     ||
            !result.Minutes.Contains(true)     ||
            !result.Hours.Contains(true)       ||
            !result.Months.Contains(true)      ||
            (options & CronExpressionOptions.IncludeSeconds) == 0 &&
            (!result.Seconds[0] || result.Seconds.AsSpan(1).Contains(true)) ||
            (options & CronExpressionOptions.AllowCalendarModifiers) == 0 &&
            (result.LastWeekday || result.LastDayOffsets.Count != 0 || result.NearestWeekdays.Count != 0 || result.LastWeekdays.Count != 0 || result.NthWeekdays.Count != 0))
        {
            throw InvalidData();
        }

        return result;
    }

    private static void ReadBits(ReadOnlySpan<byte> bytes, ref int index, bool[] values)
    {
        for (var bit = 0; bit < values.Length; bit++)
        {
            values[bit] = (bytes[index + bit / 8] & 1 << bit % 8) != 0;
        }

        var count     = (values.Length + 7) / 8;
        var remainder = values.Length       % 8;
        if (remainder != 0 && bytes[index + count - 1] >> remainder != 0)
        {
            throw InvalidData();
        }

        index += count;
    }

    private static void ReadSet(ReadOnlySpan<byte> bytes, ref int index, List<int> target, int size)
    {
        var mask = ReadMask(bytes, ref index, size);
        while (mask != 0)
        {
            target.Add(BitOperations.TrailingZeroCount(mask));
            mask &= mask - 1;
        }
    }

    private static ulong ReadMask(ReadOnlySpan<byte> bytes, ref int index, int size)
    {
        var count = (size + 7) / 8;
        var mask  = 0UL;
        for (var offset = 0; offset < count; offset++)
        {
            mask |= (ulong)bytes[index + offset] << offset * 8;
        }

        if (mask >> size != 0)
        {
            throw InvalidData();
        }

        index += count;
        return mask;
    }

    private static ArgumentException InvalidData() => new("Invalid or unsupported generated cron data.", "data");
    #endif
}
