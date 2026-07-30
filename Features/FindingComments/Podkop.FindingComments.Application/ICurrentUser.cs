namespace Podkop.FindingComments.Application;

/// <summary>
///     The identity every interaction acts as. Until real authentication exists this is a
///     hardcoded stub user supplied by the composition root — deliberately one of the sample
///     authors so own-content rules are observable in the running app (issue #13). Real auth
///     later replaces exactly this seam.
/// </summary>
public interface ICurrentUser
{
    string UserName { get; }
}
