namespace Podkop.Findings.Domain;

/// <summary>
///     The justification every bury carries — a closed list of exactly five values (CONTEXT.md,
///     issue #15). A reason is stored on the vote and is never exposed publicly: bury totals and
///     bury reasons appear in no response. Dig votes carry no reason.
/// </summary>
public enum BuryReason
{
    Duplicate,
    Spam,
    FalseInformation,
    InappropriateContent,
    Unsuitable
}
