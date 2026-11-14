namespace Caprine.Durations.Generators;

internal readonly struct GeneratedSource : IEquatable<GeneratedSource>
{
    internal string HintName { get; }
    internal string Text     { get; }

    internal GeneratedSource(string hintName, string text)
    {
        HintName = hintName;
        Text     = text;
    }

    public override bool Equals(object? obj) => obj is GeneratedSource other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(HintName);

    public bool Equals(GeneratedSource other) => HintName == other.HintName && Text == other.Text;
}
