using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Bishop.App.Findings;

public static partial class FindingsHtmlRenderer
{
    [GeneratedRegex(@"^carded:#(?<n>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex CardedOutcomeRegex();

    public static string Render(string skillName, FindingsDocument document, DateTimeOffset recordedAt, string gitSha)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>");
        sb.Append(WebUtility.HtmlEncode(skillName));
        sb.Append(" — findings</title>");
        sb.Append(Css);
        sb.Append("</head><body>");

        sb.Append("<header><h1>");
        sb.Append(WebUtility.HtmlEncode(skillName));
        sb.Append("</h1><div class=\"meta\">");
        sb.Append(document.Findings.Count.ToString(CultureInfo.InvariantCulture));
        sb.Append(" finding");
        sb.Append(document.Findings.Count == 1 ? "" : "s");
        sb.Append(" · recorded ");
        sb.Append(WebUtility.HtmlEncode(recordedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)));
        if (!string.IsNullOrEmpty(gitSha))
        {
            var shortSha = gitSha.Length > 7 ? gitSha[..7] : gitSha;
            sb.Append(" · ");
            sb.Append(WebUtility.HtmlEncode(shortSha));
        }
        sb.Append("</div></header>");

        sb.Append("<table id=\"findings\"><thead><tr>");
        AppendHeader(sb, "Severity", 0);
        AppendHeader(sb, "Title", 1);
        AppendHeader(sb, "Location", 2);
        AppendHeader(sb, "Outcome", 3);
        sb.Append("</tr></thead><tbody>");

        foreach (var f in document.Findings)
            AppendRow(sb, f);

        sb.Append("</tbody></table>");
        sb.Append(SortScript);
        sb.Append("</body></html>");

        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, string label, int columnIndex)
    {
        sb.Append("<th data-col=\"");
        sb.Append(columnIndex.ToString(CultureInfo.InvariantCulture));
        sb.Append("\">");
        sb.Append(WebUtility.HtmlEncode(label));
        sb.Append("</th>");
    }

    private static void AppendRow(StringBuilder sb, Finding f)
    {
        sb.Append("<tr>");

        // Severity chip
        sb.Append("<td>");
        if (!string.IsNullOrEmpty(f.Severity))
        {
            var severityClass = SeverityClass(f.Severity);
            sb.Append("<span class=\"chip ");
            sb.Append(severityClass);
            sb.Append("\">");
            sb.Append(WebUtility.HtmlEncode(f.Severity));
            sb.Append("</span>");
        }
        sb.Append("</td>");

        // Title + expandable body
        sb.Append("<td><details><summary>");
        sb.Append(WebUtility.HtmlEncode(f.Title));
        sb.Append("</summary><pre>");
        sb.Append(WebUtility.HtmlEncode(f.Body));
        sb.Append("</pre></details></td>");

        // Location
        sb.Append("<td class=\"loc\">");
        if (!string.IsNullOrEmpty(f.Location))
            sb.Append(WebUtility.HtmlEncode(f.Location));
        sb.Append("</td>");

        // Outcome chip
        sb.Append("<td>");
        sb.Append(OutcomeChip(f.Outcome));
        sb.Append("</td>");

        sb.Append("</tr>");
    }

    private static string SeverityClass(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" or "high" => "sev-high",
        "medium" or "med" => "sev-med",
        "low" or "info" => "sev-low",
        _ => "sev-other",
    };

    private static string OutcomeChip(string outcome)
    {
        var match = CardedOutcomeRegex().Match(outcome);
        if (match.Success)
        {
            var n = match.Groups["n"].Value;
            return $"<span class=\"chip oc-carded\">#{WebUtility.HtmlEncode(n)}</span>";
        }
        return outcome switch
        {
            "dismissed" => "<span class=\"chip oc-dismissed\">dismissed</span>",
            "parked" => "<span class=\"chip oc-parked\">parked</span>",
            _ => $"<span class=\"chip\">{WebUtility.HtmlEncode(outcome)}</span>",
        };
    }

    private const string Css =
        "<style>" +
        "body{background:#0a0a0a;color:#fff;font-family:Segoe UI,system-ui,sans-serif;margin:0;padding:24px;overflow-x:hidden}" +
        "header{margin-bottom:16px}" +
        "h1{margin:0 0 4px;font-size:20px;font-weight:600}" +
        ".meta{color:#99FFFFFF;font-size:12px}" +
        "table{width:100%;border-collapse:collapse;background:#141414;border:1px solid #2a2a2a;table-layout:fixed}" +
        "th,td{padding:8px 12px;text-align:left;border-bottom:1px solid #2a2a2a;vertical-align:top;font-size:13px;overflow-wrap:anywhere}" +
        "th{background:#141414;color:#99FFFFFF;font-weight:600;cursor:pointer;user-select:none}" +
        "th:hover{color:#fff}" +
        "td.loc{font-family:Consolas,monospace;color:#99FFFFFF;font-size:12px}" +
        "details{cursor:pointer}" +
        "details summary{outline:none}" +
        "details pre{margin:8px 0 0;padding:8px;background:#0a0a0a;border:1px solid #2a2a2a;white-space:pre-wrap;font-family:Consolas,monospace;font-size:12px}" +
        ".chip{display:inline-block;padding:2px 8px;border-radius:10px;font-size:11px;font-weight:600;color:#000}" +
        ".sev-high{background:#c97a8a}" +
        ".sev-med{background:#c4a85f}" +
        ".sev-low{background:#5fa89c}" +
        ".sev-other{background:#9aa86a}" +
        ".oc-carded{background:#7fa87a}" +
        ".oc-dismissed{background:#6b8caf}" +
        ".oc-parked{background:#9a7ab8}" +
        "</style>";

    private const string SortScript =
        "<script>" +
        "document.querySelectorAll('#findings thead th').forEach(function(th){" +
        "th.addEventListener('click',function(){" +
        "var tbody=th.closest('table').querySelector('tbody');" +
        "var rows=Array.from(tbody.querySelectorAll('tr'));" +
        "var col=parseInt(th.dataset.col,10);" +
        "var asc=th.dataset.asc!=='1';" +
        "rows.sort(function(a,b){" +
        "var av=a.children[col].innerText.trim().toLowerCase();" +
        "var bv=b.children[col].innerText.trim().toLowerCase();" +
        "return asc?av.localeCompare(bv):bv.localeCompare(av);" +
        "});" +
        "th.dataset.asc=asc?'1':'0';" +
        "rows.forEach(function(r){tbody.appendChild(r);});" +
        "});" +
        "});" +
        "</script>";
}
