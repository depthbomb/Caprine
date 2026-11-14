namespace Caprine.Durations;

/// <summary>Applies calendar durations through the built-in date and time types.</summary>
public static class DurationExtensions
{
    extension(DateTime start)
    {
        /// <summary>Adds years, months, then the fixed interval.</summary>
        /// <param name="duration">The period to apply.</param>
        /// <returns>The resulting date and time.</returns>
        public DateTime Add(CalendarDuration duration) => duration.AddTo(start);
    }

    extension(DateTimeOffset start)
    {
        /// <summary>Adds a period while retaining the numeric UTC offset.</summary>
        /// <param name="duration">The period to apply.</param>
        /// <returns>The resulting date and time.</returns>
        public DateTimeOffset Add(CalendarDuration duration) => duration.AddTo(start);
    }

    extension(DateOnly start)
    {
        /// <summary>Adds a period whose fixed portion is a whole number of days.</summary>
        /// <param name="duration">The period to apply.</param>
        /// <returns>The resulting date.</returns>
        public DateOnly Add(CalendarDuration duration) => duration.AddTo(start);
    }

    extension(TimeOnly start)
    {
        /// <summary>Adds a fixed-only period, wrapping across midnight.</summary>
        /// <param name="duration">The period to apply.</param>
        /// <returns>The resulting time.</returns>
        public TimeOnly Add(CalendarDuration duration) => duration.AddTo(start);

        /// <summary>Adds a fixed-only period and reports crossed days.</summary>
        /// <param name="duration">The period to apply.</param>
        /// <param name="wrappedDays">The signed number of crossed days.</param>
        /// <returns>The resulting time.</returns>
        public TimeOnly Add(CalendarDuration duration, out int wrappedDays) => duration.AddTo(start, out wrappedDays);
    }
}
