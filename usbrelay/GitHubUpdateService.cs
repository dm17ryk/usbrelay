using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace usbrelay
{
    public sealed class AvailableUpdate
    {
        public Version Version { get; internal set; }
        public string AssetName { get; internal set; }
        public Uri DownloadUrl { get; internal set; }
        public long Size { get; internal set; }
        public string Sha256 { get; internal set; }
    }

    public interface IUpdateService : IDisposable
    {
        Task<AvailableUpdate> CheckAsync(CancellationToken cancellationToken);
        Task<string> DownloadAsync(AvailableUpdate update, string stagingRoot, CancellationToken cancellationToken);
    }

    public sealed class GitHubUpdateService : IUpdateService
    {
        public const string ReleasesApi = "https://api.github.com/repos/dm17ryk/usbrelay/releases/latest";
        private const long MaximumInstallerSize = 256 * 1024 * 1024;
        private readonly HttpClient client;

        public GitHubUpdateService(HttpMessageHandler handler = null)
        {
            client = handler == null ? new HttpClient() : new HttpClient(handler);
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("usbrelay/" + CurrentVersion);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        public static Version CurrentVersion => typeof(GitHubUpdateService).Assembly.GetName().Version;
        public static string StagingRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "usbrelay", "updates");

        public async Task<AvailableUpdate> CheckAsync(CancellationToken cancellationToken)
        {
            Trace.WriteLine("[Updater] Checking " + ReleasesApi + "; current=" + CurrentVersion);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                using (var response = await client.GetAsync(ReleasesApi, timeout.Token).ConfigureAwait(false))
                {
                    Trace.WriteLine("[Updater] Release HTTP status=" + (int)response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        Trace.WriteLine("[Updater] No published stable release.");
                        return null;
                    }
                    response.EnsureSuccessStatusCode();
                    return ParseRelease(await response.Content.ReadAsStringAsync().ConfigureAwait(false), CurrentVersion);
                }
            }
        }

        public static AvailableUpdate ParseRelease(string json, Version currentVersion)
        {
            Release release;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                release = (Release)new DataContractJsonSerializer(typeof(Release)).ReadObject(stream);
            if (release == null) throw new InvalidDataException("GitHub returned an empty release.");
            if (release.Draft || release.Prerelease)
            {
                Trace.WriteLine("[Updater] Ignoring draft or prerelease: " + release.Tag);
                return null;
            }
            Version version;
            string tag = release.Tag ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out version))
                throw new InvalidDataException("The release tag is not a supported version: " + tag);
            var normalized = new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
            if (normalized <= currentVersion)
            {
                Trace.WriteLine("[Updater] Already current; latest=" + normalized + ", installed=" + currentVersion);
                return null;
            }
            string assetName = "usbrelay-setup-v" + tag.TrimStart('v', 'V') + ".exe";
            Asset asset = (release.Assets ?? new List<Asset>()).SingleOrDefault(item => item != null && item.Name == assetName);
            if (asset == null) throw new InvalidDataException("The latest release has no Windows installer: " + assetName);
            Uri url;
            string expectedPath = "/dm17ryk/usbrelay/releases/download/" + tag + "/" + assetName;
            if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out url) || url.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(url.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                url.AbsolutePath != expectedPath || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.UserInfo))
                throw new InvalidDataException("The release installer URL is not trusted.");
            if (asset.Size <= 0 || asset.Size > MaximumInstallerSize)
                throw new InvalidDataException("The release installer size is invalid.");
            string digest = asset.Digest ?? "";
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || !IsSha256(digest.Substring(7)))
                throw new InvalidDataException("The release installer has no valid SHA-256 digest.");
            Trace.WriteLine("[Updater] Update available=" + normalized + ", asset=" + assetName + ", bytes=" + asset.Size);
            return new AvailableUpdate { Version = normalized, AssetName = assetName, DownloadUrl = url,
                Size = asset.Size, Sha256 = digest.Substring(7).ToLowerInvariant() };
        }

        public async Task<string> DownloadAsync(AvailableUpdate update, string stagingRoot, CancellationToken cancellationToken)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            string directory = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, update.AssetName);
            Trace.WriteLine("[Updater] Download starting: " + update.DownloadUrl + ", destination=" + path);
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeSpan.FromMinutes(5));
                    using (var response = await client.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        long? contentLength = response.Content.Headers.ContentLength;
                        if (contentLength.HasValue && contentLength.Value != update.Size)
                            throw new InvalidDataException("The installer download size differs from the release.");
                        using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var destination = File.Create(path))
                        {
                            var buffer = new byte[81920];
                            long total = 0;
                            int read;
                            while ((read = await source.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                            {
                                total += read;
                                if (total > update.Size) throw new InvalidDataException("The installer download is larger than expected.");
                                await destination.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
                            }
                            if (total != update.Size) throw new InvalidDataException("The installer download is incomplete.");
                        }
                    }
                }
                VerifyInstaller(path, update.Sha256);
                Trace.WriteLine("[Updater] Download complete; size and SHA-256 verified: " + path);
                return path;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("[Updater] Download rejected: " + ex);
                try { if (File.Exists(path)) File.Delete(path); Directory.Delete(directory); }
                catch (Exception cleanupError) { Trace.WriteLine("[Updater] Failed download cleanup: " + cleanupError); }
                throw;
            }
        }

        public static void VerifyInstaller(string path, string expectedSha256)
        {
            using (var stream = File.OpenRead(path)) VerifyInstaller(stream, expectedSha256);
        }

        public static void VerifyInstaller(Stream stream, string expectedSha256)
        {
            if (!IsSha256(expectedSha256)) throw new InvalidDataException("Invalid installer SHA-256.");
            string actual;
            using (var hash = SHA256.Create())
                actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The installer SHA-256 verification failed.");
            Trace.WriteLine("[Updater] SHA-256 verified: " + actual);
        }

        private static bool IsSha256(string value) => value != null && value.Length == 64 &&
            value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f' || character >= 'A' && character <= 'F');

        public void Dispose() => client.Dispose();

        [DataContract]
        private sealed class Release
        {
            [DataMember(Name = "tag_name")] public string Tag { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
            [DataMember(Name = "assets")] public List<Asset> Assets { get; set; }
        }

        [DataContract]
        private sealed class Asset
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "browser_download_url")] public string Url { get; set; }
            [DataMember(Name = "size")] public long Size { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
        }
    }
}
