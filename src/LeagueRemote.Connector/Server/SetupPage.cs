using System.Net;

namespace LeagueRemote.Connector.Server;

/// <summary>Self-contained (no external resources) first-time phone setup page served at <c>/setup</c> over plain HTTP.</summary>
internal static class SetupPage
{
    public static string Render(string appUrl, string caCertificatePath, string caFingerprint, string caName)
    {
        var app = WebUtility.HtmlEncode(appUrl);
        var ca = WebUtility.HtmlEncode(caCertificatePath);
        var fingerprint = WebUtility.HtmlEncode(caFingerprint);
        var caLabel = WebUtility.HtmlEncode(caName);
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="color-scheme" content="light dark">
            <title>League Remote – phone setup</title>
            <style>
              /* Colours from web/design/tokens.json (bg, bg-elevated, label, label-secondary, accent, on-accent, separator). */
              :root { color-scheme:light dark; --bg:#f2f2f7; --card:#fff; --text:#000; --muted:#6c6c70; --accent:#cc9a30; --on-accent:#1f1606; --border:rgba(60,60,67,.29); }
              @media (prefers-color-scheme: dark) {
                :root { --bg:#000; --card:#1c1c1e; --text:#fff; --muted:#98989f; --accent:#e6b95c; --border:rgba(84,84,88,.65); }
              }
              * { box-sizing: border-box; }
              body { margin:0; background:var(--bg); color:var(--text); font:16px/1.5 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif; }
              main { max-width:560px; margin:0 auto; padding:24px 16px 40px; }
              h1 { font-size:1.5rem; margin:0 0 4px; }
              h2 { font-size:1.05rem; margin:0 0 8px; }
              p, li { color:var(--text); }
              .muted { color:var(--muted); font-size:.875rem; }
              section { background:var(--card); border:1px solid var(--border); border-radius:12px; padding:16px; margin:16px 0; }
              ol { margin:0; padding-left:1.25rem; }
              li { margin:4px 0; }
              .btn { display:block; text-align:center; text-decoration:none; font-weight:600; border-radius:10px; padding:14px 16px; margin:12px 0 0; }
              .primary { background:var(--accent); color:var(--on-accent); }
              .secondary { background:transparent; color:var(--text); border:1px solid var(--border); }
              code { font:12px/1.4 ui-monospace,Consolas,monospace; word-break:break-all; color:var(--muted); }
              footer { margin-top:24px; }
            </style>
            </head>
            <body>
            <main>
              <h1>League Remote</h1>
              <p class="muted">First time on this phone? Install the PC's certificate once, then open the app over HTTPS.</p>

              <section>
                <h2>1. Download the certificate</h2>
                <a class="btn secondary" href="{{ca}}">Download league-remote-ca.crt</a>
                <p class="muted">It was created on your PC and only identifies the League Remote app there. SHA-256 fingerprint:<br><code>{{fingerprint}}</code></p>
              </section>

              <section>
                <h2>2. Install it</h2>
                <p><strong>Android</strong></p>
                <ol>
                  <li>Settings → Security → Encryption &amp; credentials → Install a certificate → <strong>CA certificate</strong>.</li>
                  <li>Confirm "Install anyway" and pick the downloaded <code>league-remote-ca.crt</code>.</li>
                </ol>
                <p class="muted">Menu names vary a little by manufacturer; search Settings for "certificate".</p>
                <p><strong>iPhone / iPad</strong></p>
                <ol>
                  <li>Open this page in <strong>Safari</strong> and tap the download link above; allow the configuration profile.</li>
                  <li>Settings → <strong>Profile Downloaded</strong> → Install.</li>
                  <li>Settings → General → About → <strong>Certificate Trust Settings</strong> → enable full trust for "{{caLabel}}".</li>
                </ol>
              </section>

              <section>
                <h2>3. Open the app</h2>
                <a class="btn primary" href="{{app}}">Open League Remote (HTTPS)</a>
                <p class="muted">Then pair by scanning the QR code in the PC's tray window, or type the 6-digit code. Tip: add it to your home screen.</p>
              </section>

              <footer class="muted">
                League Remote is not endorsed by Riot Games and does not reflect the views or opinions of Riot Games or anyone officially
                involved in producing or managing Riot Games properties. Riot Games and all associated properties are trademarks or
                registered trademarks of Riot Games, Inc.
              </footer>
            </main>
            </body>
            </html>
            """;
    }
}
