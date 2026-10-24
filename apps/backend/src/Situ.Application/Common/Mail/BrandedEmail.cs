using System.Text;

namespace Situ.Application.Common.Mail;

internal readonly record struct EmailButton(string Label, string Url);

internal static class BrandedEmail
{
    public static string Render(
        string preheader,
        string greeting,
        IReadOnlyList<string> paragraphs,
        EmailButton? button = null,
        IReadOnlyList<string>? lines = null)
    {
        var html = new StringBuilder();
        html.Append("""
            <!DOCTYPE html>
            <html lang="es">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="color-scheme" content="dark">
            <title>SITU</title>
            </head>
            <body style="margin:0;padding:0;background-color:#1C1C1C;">
            """);
        html.Append("<div style=\"display:none;max-height:0;overflow:hidden;color:#1C1C1C;\">");
        html.Append(Encode(preheader));
        html.Append("</div>");
        html.Append("""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#1C1C1C;">
            <tr><td align="center" style="padding:32px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;background-color:#242424;border:1px solid #3A3A38;border-radius:16px;">
            <tr><td style="padding:28px 28px 0;font-family:Montserrat,Arial,sans-serif;font-size:22px;line-height:28px;font-weight:800;letter-spacing:1px;color:#FF6400;">SITU</td></tr>
            """);
        html.Append("<tr><td style=\"padding:18px 28px 0;font-family:'Source Sans 3',Arial,sans-serif;font-size:18px;line-height:26px;font-weight:700;color:#F2F2F2;\">");
        html.Append(Encode(greeting));
        html.Append("</td></tr>");

        foreach (var paragraph in paragraphs)
        {
            html.Append("<tr><td style=\"padding:12px 28px 0;font-family:'Source Sans 3',Arial,sans-serif;font-size:16px;line-height:24px;color:#A8A8A8;\">");
            html.Append(Encode(paragraph));
            html.Append("</td></tr>");
        }

        if (lines is { Count: > 0 })
        {
            html.Append("<tr><td style=\"padding:16px 28px 0;\">");
            foreach (var line in lines)
            {
                html.Append("<p style=\"margin:0 0 8px;font-family:'Source Sans 3',Arial,sans-serif;font-size:15px;line-height:22px;color:#F2F2F2;\">");
                html.Append(Encode(line));
                html.Append("</p>");
            }

            html.Append("</td></tr>");
        }

        if (button is { } action)
        {
            var url = Encode(action.Url);
            html.Append("<tr><td align=\"left\" style=\"padding:24px 28px 0;\">");
            html.Append("<a href=\"");
            html.Append(url);
            html.Append("\" style=\"display:inline-block;background-color:#FF6400;color:#141414;font-family:'Source Sans 3',Arial,sans-serif;font-size:16px;line-height:20px;font-weight:700;text-decoration:none;padding:14px 22px;border-radius:8px;\">");
            html.Append(Encode(action.Label));
            html.Append("</a></td></tr>");
            html.Append("<tr><td style=\"padding:16px 28px 0;font-family:'Source Sans 3',Arial,sans-serif;font-size:13px;line-height:20px;color:#7A7A7A;\">Si el botón no abre, copia este enlace:<br><a href=\"");
            html.Append(url);
            html.Append("\" style=\"color:#FF9F24;word-break:break-all;\">");
            html.Append(url);
            html.Append("</a></td></tr>");
        }

        html.Append("""
            <tr><td style="padding:28px;font-family:'Source Sans 3',Arial,sans-serif;font-size:12px;line-height:18px;color:#7A7A7A;">SITU · Lo que pasa, donde pasa.</td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """);
        return html.ToString();
    }

    private static string Encode(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
