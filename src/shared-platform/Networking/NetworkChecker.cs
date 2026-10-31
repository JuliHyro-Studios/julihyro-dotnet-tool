namespace JuliHyro.DotNetTool.Shared.Networking;

/// <summary>
/// Checks whether the machine can reach Microsoft's .NET release metadata endpoint.
/// </summary>
public sealed class NetworkChecker(HttpClient httpClient)
{
    private static readonly Uri ProbeUri = new("https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json");

    public async Task<bool> HasInternetAccessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, ProbeUri);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
                return true;

            // Some storage endpoints do not implement HEAD consistently.
            using var get = await httpClient.GetAsync(ProbeUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return get.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }
}
