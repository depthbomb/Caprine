namespace Caprine.FilePath;

/// <summary>Specifies the rules used to interpret a filesystem path.</summary>
public enum PathFlavor
{
    /// <summary>Use the current operating system's rules.</summary>
    Native,
    /// <summary>Use Windows drive, UNC, and backslash rules.</summary>
    Windows,
    /// <summary>Use POSIX slash rules, treating backslashes as ordinary characters.</summary>
    Posix
}
