using Podkop.FindingComments.Application;

namespace Podkop.Server;

/// <summary>
///     The hardcoded stub identity every interaction acts as until real authentication exists
///     (issue #13) — deliberately one of the five sample authors so own-content rules are
///     observable in the running app. Real auth later replaces exactly this registration.
/// </summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public string UserName => "ada_lovelace";
}
