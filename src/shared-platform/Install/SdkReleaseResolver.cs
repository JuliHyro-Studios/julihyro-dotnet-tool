using System.Text.Json;
using JuliHyro.DotNetTool.Shared.Models;

namespace JuliHyro.DotNetTool.Shared.Install;

/// <summary>
/// Resolves exact versions and major.minor channels using Microsoft's release metadata.
/// </summary>
public sealed class SdkReleaseResolver(HttpClient httpClient)
{
    private const string IndexUrl = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json";

    public async Task<SdkRelease> ResolveAsync(string requestedVersion, CancellationToken cancellationToken = default)
    {
        var input = requestedVersion.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(input, @"^\d+\.\d+(\.\d+)?$"))
            throw new ArgumentException("Enter an SDK version such as 10.0 or 10.0.403.", nameof(requestedVersion));

        using var indexStream = await httpClient.GetStreamAsync(IndexUrl, cancellationToken);
        using var index = await JsonDocument.ParseAsync(indexStream, cancellationToken: cancellationToken);

        var channel = input.Split('.')[0] + "." + input.Split('.')[1];
        var channelUrl = index.RootElement.EnumerateArray()
            .Where(x => x.TryGetProperty("channel-version", out var v) && v.GetString() == channel)
            .Select(x => x.GetProperty("releases.json").GetString())
            .FirstOrDefault();

        if (channelUrl is null)
            throw new InvalidOperationException($"The .NET channel {channel} was not found in Microsoft's release index.");

        using var releasesStream = await httpClient.GetStreamAsync(channelUrl, cancellationToken);
        using var releases = await JsonDocument.ParseAsync(releasesStream, cancellationToken: cancellationToken);

        var candidates = new List<(Version Version, string Text, Uri Uri)>();
        foreach (var release in releases.RootElement.GetProperty("releases").EnumerateArray())
        {
            if (!release.TryGetProperty("sdk", out var sdk) ||
                !sdk.TryGetProperty("version", out var versionNode))
                continue;

            var versionText = versionNode.GetString();
            if (string.IsNullOrWhiteSpace(versionText) || !Version.TryParse(versionText, out var parsed))
                continue;

            if (input.Count(c => c == '.') == 2 && !string.Equals(input, versionText, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!sdk.TryGetProperty("files", out var files))
                continue;

            var installer = files.EnumerateArray().FirstOrDefault(f =>
                f.TryGetProperty("rid", out var rid) && rid.GetString() == "win-x64" &&
                f.TryGetProperty("name", out var name) &&
                name.GetString()!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (installer.ValueKind == JsonValueKind.Undefined ||
                !installer.TryGetProperty("url", out var urlNode) ||
                !Uri.TryCreate(urlNode.GetString(), UriKind.Absolute, out var uri))
                continue;

            candidates.Add((parsed, versionText, uri));
        }

        var selected = candidates.OrderByDescending(x => x.Version).FirstOrDefault();
        if (selected.Uri is null)
            throw new InvalidOperationException($"No Windows x64 SDK installer was found for {input}.");

        return new SdkRelease(selected.Text, selected.Uri);
    }
}
