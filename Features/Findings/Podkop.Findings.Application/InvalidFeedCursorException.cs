namespace Podkop.Findings.Application;

public sealed class InvalidFeedCursorException(string cursor, Exception? innerException = null)
    : Exception($"'{cursor}' is not a valid feed cursor.", innerException);