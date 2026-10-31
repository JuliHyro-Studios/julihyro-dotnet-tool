using System.Text.Json;
using System.Text.RegularExpressions;
using JuliHyro.DotNetTool.Shared.Models;

namespace JuliHyro.DotNetTool.Shared.Install;

/// <summary>
/// Resolves exact SDK versions and major.minor channels using Microsoft's release metadata.
/// </summary>
public sealed class SdkReleaseResolver(HttpClient httpClient)
{
    private const string IndexUrl = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json";

    public async Task<SdkRelease> ResolveAsync(
        string requestedVersion,
        CancellationToken cancellationToken = default)
    {
        var input = NormalizeRequestedVersion(requestedVersion);
        var parts = input.Split('.');
        var channel = $"{parts[0]}.{parts[1]}";

        using var indexStream = await httpClient.GetStreamAsync(IndexUrl, cancellationToken);
        using var index = await JsonDocument.ParseAsync(
            indexStream,
            cancellationToken: cancellationToken);

        JsonElement indexReleases;

        if (index.RootElement.ValueKind == JsonValueKind.Array)
        {
            // releases-index.json is currently a JSON array of channel descriptors.
            indexReleases = index.RootElement;
        }
        else if (index.RootElement.ValueKind == JsonValueKind.Object &&
                 index.RootElement.TryGetProperty("releases", out var nestedReleases) &&
                 nestedReleases.ValueKind == JsonValueKind.Array)
        {
            // Also accept an object-wrapped index for forward/backward compatibility.
            indexReleases = nestedReleases;
        }
        else
        {
            throw new InvalidOperationException(
                "Microsoft's .NET release index has an unexpected JSON structure: " +
                "expected an array of channel descriptors.");
        }

        string? channelUrl = null;

        foreach (var item in indexReleases.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("channel-version", out var channelNode) ||
                channelNode.ValueKind != JsonValueKind.String ||
                !string.Equals(channelNode.GetString(), channel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.TryGetProperty("releases.json", out var releasesUrlNode) &&
                releasesUrlNode.ValueKind == JsonValueKind.String)
            {
                channelUrl = releasesUrlNode.GetString();
            }

            break;
        }

        if (string.IsNullOrWhiteSpace(channelUrl) ||
            !Uri.TryCreate(channelUrl, UriKind.Absolute, out var releasesUri) ||
            (releasesUri.Scheme != Uri.UriSchemeHttps && releasesUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException(
                $"Microsoft's .NET release index did not provide a valid releases.json URL for channel {channel}.");
        }

        using var releasesStream = await httpClient.GetStreamAsync(releasesUri, cancellationToken);
        using var releases = await JsonDocument.ParseAsync(
            releasesStream,
            cancellationToken: cancellationToken);

        if (releases.RootElement.ValueKind != JsonValueKind.Object ||
            !releases.RootElement.TryGetProperty("releases", out var channelReleases) ||
            channelReleases.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"Microsoft's release metadata for channel {channel} has an unexpected JSON structure: " +
                "the root 'releases' array was not found.");
        }

        var exactVersionRequested = parts.Length == 3;
        var candidates = new List<(Version Version, string Text, Uri Uri)>();

        foreach (var release in channelReleases.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object ||
                !release.TryGetProperty("sdk", out var sdk) ||
                sdk.ValueKind != JsonValueKind.Object ||
                !sdk.TryGetProperty("version", out var versionNode) ||
                versionNode.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var versionText = versionNode.GetString();
            if (string.IsNullOrWhiteSpace(versionText) ||
                !Version.TryParse(versionText, out var parsedVersion))
            {
                continue;
            }

            if (exactVersionRequested &&
                !string.Equals(input, versionText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!sdk.TryGetProperty("files", out var files) ||
                files.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var file in files.EnumerateArray())
            {
                if (file.ValueKind != JsonValueKind.Object ||
                    !file.TryGetProperty("rid", out var ridNode) ||
                    ridNode.ValueKind != JsonValueKind.String ||
                    !string.Equals(ridNode.GetString(), "win-x64", StringComparison.OrdinalIgnoreCase) ||
                    !file.TryGetProperty("name", out var nameNode) ||
                    nameNode.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var fileName = nameNode.GetString();
                if (string.IsNullOrWhiteSpace(fileName) ||
                    !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    !file.TryGetProperty("url", out var urlNode) ||
                    urlNode.ValueKind != JsonValueKind.String ||
                    !Uri.TryCreate(urlNode.GetString(), UriKind.Absolute, out var installerUri) ||
                    (installerUri.Scheme != Uri.UriSchemeHttps && installerUri.Scheme != Uri.UriSchemeHttp))
                {
                    continue;
                }

                candidates.Add((parsedVersion, versionText, installerUri));
                break;
            }
        }

        var selected = candidates
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefault();

        if (selected.Uri is null)
        {
            throw new InvalidOperationException(
                exactVersionRequested
                    ? $"The exact SDK version {input} does not have a Windows x64 installer in Microsoft's release metadata."
                    : $"No Windows x64 SDK installer was found for .NET channel {channel}.");
        }

        return new SdkRelease(selected.Text, selected.Uri);
    }

    private static string NormalizeRequestedVersion(string requestedVersion)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            throw new ArgumentException(
                "Enter an SDK version such as 9, 9.0 or 9.0.100.",
                nameof(requestedVersion));
        }

        var input = requestedVersion.Trim();

        if (Regex.IsMatch(input, @"^\d+$"))
        {
            input += ".0";
        }

        if (!Regex.IsMatch(input, @"^\d+\.\d+(\.\d+)?$"))
        {
            throw new ArgumentException(
                "Enter an SDK version such as 9, 9.0 or 9.0.100.",
                nameof(requestedVersion));
        }

        return input;
    }
}
