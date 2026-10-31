using System.Text.Json;
using System.Text.RegularExpressions;
using JuliHyro.DotNetTool.Shared.Models;

namespace JuliHyro.DotNetTool.Shared.Install;

/// <summary>
/// Resolves exact SDK versions and major.minor channels using Microsoft's release metadata.
/// The parser tolerates common JSON wrapper and property-shape variations.
/// </summary>
public sealed class SdkReleaseResolver(HttpClient httpClient)
{
    private static readonly string[] IndexUrls =
    [
        "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json",
        "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json",
        "https://dotnetcli.azureedge.net/dotnet/release-metadata/releases-index.json"
    ];

    private static readonly string[] MetadataBases =
    [
        "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata",
        "https://builds.dotnet.microsoft.com/dotnet/release-metadata",
        "https://dotnetcli.azureedge.net/dotnet/release-metadata"
    ];

    public async Task<SdkRelease> ResolveAsync(
        string requestedVersion,
        CancellationToken cancellationToken = default)
    {
        var input = NormalizeRequestedVersion(requestedVersion);
        var parts = input.Split('.');
        var channel = $"{parts[0]}.{parts[1]}";
        var exactVersionRequested = parts.Length == 3;

        var metadataUrls = await FindChannelMetadataUrlsAsync(channel, cancellationToken);
        foreach (var baseUrl in MetadataBases)
        {
            var directUrl = $"{baseUrl}/{channel}/releases.json";
            if (!metadataUrls.Contains(directUrl, StringComparer.OrdinalIgnoreCase))
                metadataUrls.Add(directUrl);
        }

        var failures = new List<string>();
        foreach (var metadataUrl in metadataUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await httpClient.GetAsync(
                    metadataUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add($"{metadataUrl} returned HTTP {(int)response.StatusCode}");
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);

                var candidates = ExtractSdkCandidates(document.RootElement, exactVersionRequested ? input : null);
                var selected = candidates
                    .OrderByDescending(candidate => candidate.Version)
                    .FirstOrDefault();

                if (selected.Uri is not null)
                    return new SdkRelease(selected.Text, selected.Uri);

                failures.Add($"{metadataUrl} contained no matching Windows x64 SDK installer");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                failures.Add($"{metadataUrl}: {ex.Message}");
            }
            catch (JsonException ex)
            {
                failures.Add($"{metadataUrl}: invalid JSON ({ex.Message})");
            }
            catch (IOException ex)
            {
                failures.Add($"{metadataUrl}: {ex.Message}");
            }
        }

        var detail = failures.Count == 0
            ? "No usable Microsoft release metadata endpoint was found."
            : string.Join(System.Environment.NewLine, failures.Select(failure => $" - {failure}"));

        throw new InvalidOperationException(
            exactVersionRequested
                ? $"Could not resolve SDK {input} from Microsoft's release metadata.{System.Environment.NewLine}{detail}"
                : $"Could not resolve a Windows x64 SDK installer for .NET channel {channel}.{System.Environment.NewLine}{detail}");
    }

    private async Task<List<string>> FindChannelMetadataUrlsAsync(
        string channel,
        CancellationToken cancellationToken)
    {
        var urls = new List<string>();

        foreach (var indexUrl in IndexUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await httpClient.GetAsync(
                    indexUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    continue;

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);

                CollectChannelUrls(document.RootElement, channel, urls);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                // Try the next official Microsoft metadata mirror.
            }
            catch (JsonException)
            {
                // A mirror may return a wrapper/error document. Other mirrors and
                // the deterministic channel URLs are still attempted.
            }
            catch (IOException)
            {
                // Continue to the next official endpoint.
            }

            if (urls.Count > 0)
                break;
        }

        return urls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectChannelUrls(
        JsonElement element,
        string requestedChannel,
        List<string> urls)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var channelValue = GetStringProperty(element,
                "channel-version", "channelVersion", "channel", "version");

            if (string.Equals(channelValue, requestedChannel, StringComparison.OrdinalIgnoreCase))
            {
                var releaseUrl = GetStringProperty(element,
                    "releases.json", "releasesJson", "releases-url", "releasesUrl");

                if (IsHttpUrl(releaseUrl))
                    urls.Add(releaseUrl!);
            }

            foreach (var property in element.EnumerateObject())
                CollectChannelUrls(property.Value, requestedChannel, urls);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectChannelUrls(item, requestedChannel, urls);
        }
    }

    private static List<(Version Version, string Text, Uri Uri)> ExtractSdkCandidates(
        JsonElement root,
        string? exactVersion)
    {
        var candidates = new List<(Version Version, string Text, Uri Uri)>();
        var releaseNodes = new List<JsonElement>();

        CollectReleaseArrays(root, releaseNodes);

        foreach (var release in releaseNodes)
        {
            var sdkNodes = new List<JsonElement>();
            CollectNamedObjects(release, sdkNodes, "sdk");

            foreach (var sdk in sdkNodes)
            {
                var versionText = GetStringProperty(sdk, "version", "sdk-version", "sdkVersion");
                if (string.IsNullOrWhiteSpace(versionText) ||
                    !Version.TryParse(versionText, out var parsedVersion))
                {
                    continue;
                }

                if (exactVersion is not null &&
                    !string.Equals(exactVersion, versionText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var filesNodes = new List<JsonElement>();
                CollectNamedArrays(sdk, filesNodes, "files", "artifacts", "downloads");

                foreach (var files in filesNodes)
                {
                    foreach (var file in files.EnumerateArray())
                    {
                        if (file.ValueKind != JsonValueKind.Object)
                            continue;

                        var rid = GetStringProperty(file, "rid", "runtimeIdentifier", "runtime-identifier");
                        if (!string.Equals(rid, "win-x64", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var fileName = GetStringProperty(file, "name", "fileName", "filename");
                        var url = GetStringProperty(file, "url", "downloadUrl", "download-url");

                        if (!IsHttpUrl(url))
                            continue;

                        if (!string.IsNullOrWhiteSpace(fileName) &&
                            !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (Uri.TryCreate(url, UriKind.Absolute, out var installerUri))
                            candidates.Add((parsedVersion, versionText, installerUri));
                    }
                }
            }
        }

        return candidates;
    }

    private static void CollectReleaseArrays(JsonElement element, List<JsonElement> arrays)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsName(property.Name, "releases", "releaseList", "release-list") &&
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    arrays.Add(property.Value);
                }

                CollectReleaseArrays(property.Value, arrays);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            // Some metadata variants expose the release entries directly as root arrays.
            if (element.EnumerateArray().Any(item =>
                    item.ValueKind == JsonValueKind.Object &&
                    (HasProperty(item, "sdk") || HasProperty(item, "sdk-version") || HasProperty(item, "sdkVersion"))))
            {
                arrays.Add(element);
            }
            else
            {
                foreach (var item in element.EnumerateArray())
                    CollectReleaseArrays(item, arrays);
            }
        }
    }

    private static void CollectNamedObjects(JsonElement element, List<JsonElement> results, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsName(property.Name, names) && property.Value.ValueKind == JsonValueKind.Object)
                    results.Add(property.Value);

                CollectNamedObjects(property.Value, results, names);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectNamedObjects(item, results, names);
        }
    }

    private static void CollectNamedArrays(JsonElement element, List<JsonElement> results, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsName(property.Name, names) && property.Value.ValueKind == JsonValueKind.Array)
                    results.Add(property.Value);

                CollectNamedArrays(property.Value, results, names);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectNamedArrays(item, results, names);
        }
    }

    private static string? GetStringProperty(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in element.EnumerateObject())
        {
            if (!IsName(property.Name, names))
                continue;

            if (property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();

            if (property.Value.ValueKind == JsonValueKind.Number)
                return property.Value.GetRawText();
        }

        return null;
    }

    private static bool HasProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        return element.EnumerateObject().Any(property =>
            string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsName(string candidate, params string[] names) =>
        names.Any(name => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

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
            input += ".0";

        if (!Regex.IsMatch(input, @"^\d+\.\d+(\.\d+)?$"))
        {
            throw new ArgumentException(
                "Enter an SDK version such as 9, 9.0 or 9.0.100.",
                nameof(requestedVersion));
        }

        return input;
    }
}
