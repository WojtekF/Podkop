namespace Podkop.Findings.Application;

/// <summary>
///     The identity every finding interaction acts as. Until real authentication exists this is a
///     hardcoded stub user supplied by the composition root — deliberately one of the sample
///     authors so own-content rules are observable in the running app (issue #13). Each slice
///     owns its own current-user port (ADR 0003); this is the Findings slice's. Real auth later
///     replaces exactly this seam.
/// </summary>
public interface ICurrentUser
{
    string UserName { get; }
}
