namespace Podkop.Findings.Application;

public sealed class InvalidFeedCursorException(string cursor)
    : Exception($"'{cursor}' is not a valid feed cursor.");
