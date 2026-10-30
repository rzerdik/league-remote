using System.Net.Http.Headers;
using System.Net.Security;

namespace LeagueRemote.Connector.Lcu;

internal static class LcuHttp
{
    /// <summary>
    /// The LCU serves a certificate signed by Riot's own root, which Windows does not trust. Policy: an untrusted certificate
    /// is accepted ONLY from 127.0.0.1 (where only local processes can listen); everything else gets normal validation.
    /// </summary>
    public static bool IsTrustedCertificate(Uri? requestUri, SslPolicyErrors errors) =>
        errors == SslPolicyErrors.None ||
        string.Equals(requestUri?.Host, LcuCredentials.Host, StringComparison.Ordinal);

    public static HttpClient CreateClient(LcuCredentials credentials)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, _, _, errors) =>
                IsTrustedCertificate(message.RequestUri, errors),
        };

        var http = new HttpClient(handler)
        {
            BaseAddress = credentials.BaseUri,
            Timeout = TimeSpan.FromSeconds(10),
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials.BasicAuthParameter);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }
}
