using System.Text;
using WareConnect.Api.AI.Models;

namespace WareConnect.Api.AI.Context;

/// <inheritdoc />
public sealed class ContextBuilder : IContextBuilder
{
    public ResolvedContext Build(ScreenContext? raw)
    {
        if (raw is null)
            return new ResolvedContext();

        return new ResolvedContext
        {
            CurrentPage      = Sanitize(raw.CurrentPage),
            CurrentModule    = Sanitize(raw.CurrentModule),
            CurrentCompanyId = Sanitize(raw.CurrentCompanyId),
            CurrentCompany   = Sanitize(raw.CurrentCompany),
            CurrentVendorId  = Sanitize(raw.CurrentVendorId),
            CurrentVendor    = Sanitize(raw.CurrentVendor),
            CurrentInvoiceId = Sanitize(raw.CurrentInvoiceId),
            SelectedRowId    = Sanitize(raw.SelectedRowId),
            ActiveFilters    = raw.ActiveFilters
                                   .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                                   .ToDictionary(kv => Sanitize(kv.Key)!, kv => Sanitize(kv.Value) ?? string.Empty),
            Language         = string.IsNullOrWhiteSpace(raw.Language) ? "en" : raw.Language,
            TimeZone         = string.IsNullOrWhiteSpace(raw.TimeZone) ? "UTC" : raw.TimeZone,
        };
    }

    public string FormatForPrompt(ResolvedContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Current Screen Context");

        AppendIf(sb, "Page",        ctx.CurrentPage);
        AppendIf(sb, "Module",      ctx.CurrentModule);
        AppendIf(sb, "Company",     ctx.CurrentCompany ?? ctx.CurrentCompanyId);
        AppendIf(sb, "Vendor",      ctx.CurrentVendor  ?? ctx.CurrentVendorId);
        AppendIf(sb, "Invoice ID",  ctx.CurrentInvoiceId);
        AppendIf(sb, "Selected Row",ctx.SelectedRowId);
        AppendIf(sb, "Language",    ctx.Language);
        AppendIf(sb, "Time Zone",   ctx.TimeZone);

        if (ctx.ActiveFilters.Count > 0)
        {
            var filters = string.Join(", ", ctx.ActiveFilters.Select(kv => $"{kv.Key}={kv.Value}"));
            sb.AppendLine($"- **Active Filters:** {filters}");
        }

        sb.AppendLine();
        sb.AppendLine("> Use the context above to resolve references such as \"this invoice\", \"current vendor\", or \"these results\" without asking the user to repeat them.");

        return sb.ToString().TrimEnd();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void AppendIf(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.AppendLine($"- **{label}:** {value}");
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Strip common prompt-injection characters
        return value
            .Replace("```", string.Empty)
            .Replace("IGNORE", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
