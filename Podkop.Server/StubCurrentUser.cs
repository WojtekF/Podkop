using FindingCommentsUser = Podkop.FindingComments.Application.ICurrentUser;
using FindingsUser = Podkop.Findings.Application.ICurrentUser;

namespace Podkop.Server;

/// <summary>
///     The hardcoded stub identity every interaction acts as until real authentication exists
///     (issue #13) — deliberately one of the sample authors so own-content rules are
///     observable in the running app. It stands in for the current user of both slices that
///     have one (Findings and FindingComments); each owns its own port (ADR 0003), so a single
///     stub satisfies both. Real auth later replaces exactly this registration.
/// </summary>
internal sealed class StubCurrentUser : FindingCommentsUser, FindingsUser
{
    public string UserName => "ada_lovelace";
}
