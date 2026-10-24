using System.Net;
using System.Text;

namespace Situ.Api.Http;

public static class AuthLinkPage
{
    public static IResult Show(string title, string message, bool success, int statusCode)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeMessage = WebUtility.HtmlEncode(message);
        var messageClass = success ? "lead" : "alert";
        var html = $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="dark">
              <title>{{safeTitle}} · SITU</title>
              <link rel="preconnect" href="https://fonts.googleapis.com">
              <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
              <link href="https://fonts.googleapis.com/css2?family=Montserrat:ital,wght@1,800&family=Source+Sans+3:ital,wght@1,700&display=swap" rel="stylesheet">
              <style>
                :root { color-scheme: dark; }
                * { box-sizing: border-box; }
                body {
                  margin: 0;
                  min-height: 100dvh;
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  padding: 40px 24px;
                  background: radial-gradient(circle, #34352b 0%, #1c1c1c 72%);
                  color: #f2f2f2;
                  font-family: "Source Sans 3", sans-serif;
                }
                main { width: min(100%, 420px); text-align: center; }
                .brand {
                  margin: 0 0 28px;
                  color: #ff6400;
                  font-family: Montserrat, sans-serif;
                  font-style: italic;
                  font-weight: 800;
                  font-size: 64px;
                  line-height: 1;
                }
                h1 {
                  margin: 0;
                  font-family: Montserrat, sans-serif;
                  font-style: italic;
                  font-weight: 800;
                  font-size: 42px;
                  line-height: 1.05;
                }
                p { margin: 16px 0 0; font-style: italic; font-weight: 700; font-size: 16px; line-height: 1.5; }
                .lead { color: #a8a8a8; }
                .alert { color: #ff5a5a; }
              </style>
            </head>
            <body>
              <main>
                <p class="brand">SITU</p>
                <h1>{{safeTitle}}</h1>
                <p class="{{messageClass}}">{{safeMessage}}</p>
              </main>
            </body>
            </html>
            """;

        return Results.Text(html, "text/html; charset=utf-8", Encoding.UTF8, statusCode);
    }
}
