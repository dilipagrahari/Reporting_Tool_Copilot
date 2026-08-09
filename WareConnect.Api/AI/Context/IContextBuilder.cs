using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Context;

/// <summary>
/// Builds a <see cref="ResolvedContext"/> from the raw <see cref="ScreenContext"/> sent by Angular.
/// Ensures the AI never has to infer what the user is looking at.
/// </summary>
public interface IContextBuilder
{
    /// <summary>
    /// Resolves and enriches the screen context for inclusion in the AI prompt.
    /// Falls back to safe defaults when a field is absent.
    /// </summary>
    ResolvedContext Build(ScreenContext? raw);

    /// <summary>Serialises a <see cref="ResolvedContext"/> into a prompt-friendly text block.</summary>
    string FormatForPrompt(ResolvedContext ctx);
}
