using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HolidayTimer
{
    internal sealed class AvailableUpdate
    {
        internal Version Version;
        internal string Tag, Sha256;
        internal long Size;
        internal Uri DownloadUrl, ReleaseUrl;
    }

    // A release points to one specific, bounded installer. The GitHub digest
    // verifies transfer integrity; it is not a code-signing identity.
    internal sealed class GitHubReleaseClient
    {
        private const int MaximumMetadataBytes = 512 * 1024;
        private const long MaximumInstallerBytes = 64L * 1024 * 1024;
        private const string AssetName = "HolidayTimerSetup.exe";
        private readonly string owner, repository;

        // The serializer populates these fields from GitHub's JSON.
#pragma warning disable 0649
        [DataContract] private sealed class ReleaseRecord
        {
            [DataMember(Name = "tag_name", IsRequired = true)] internal string Tag;
            [DataMember(Name = "draft", IsRequired = true)] internal bool Draft;
            [DataMember(Name = "prerelease", IsRequired = true)] internal bool Prerelease;
            [DataMember(Name = "assets", IsRequired = true)] internal AssetRecord[] Assets;
        }
        [DataContract] private sealed class AssetRecord
        {
            [DataMember(Name = "name")] internal string Name;
            [DataMember(Name = "state")] internal string State;
            [DataMember(Name = "browser_download_url")] internal string Url;
            [DataMember(Name = "digest")] internal string Digest;
            [DataMember(Name = "size")] internal long Size;
        }
#pragma warning restore 0649

        internal GitHubReleaseClient(string owner, string repository)
        {
            if (String.IsNullOrEmpty(owner) || !Regex.IsMatch(owner, @"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$"))
                throw new ArgumentException("Invalid GitHub owner.");
            if (String.IsNullOrEmpty(repository) || repository.Length > 100 || repository == "." || repository == ".." || !Regex.IsMatch(repository, @"^[A-Za-z0-9_.-]+$"))
                throw new ArgumentException("Invalid GitHub repository.");
            this.owner = owner; this.repository = repository;
        }

        internal Uri ApiUrl { get { return new Uri("https://api.github.com/repos/" + owner + "/" + repository + "/releases/latest"); } }
        internal Uri ReleasesUrl { get { return new Uri("https://github.com/" + owner + "/" + repository + "/releases"); } }

        internal AvailableUpdate Check()
        {
            HttpWebRequest request = CreateMetadataRequest(ApiUrl);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > MaximumMetadataBytes)
                    throw new InvalidDataException("GitHub returned an invalid release response.");
                using (Stream stream = response.GetResponseStream())
                    return ParseLatest(ReadBounded(stream, MaximumMetadataBytes), owner, repository);
            }
        }

        internal static HttpWebRequest CreateMetadataRequest(Uri address)
        {
            HttpWebRequest request = NewRequest(address);
            request.Accept = "application/vnd.github+json";
            request.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            return request;
        }

        internal static AvailableUpdate ParseLatest(byte[] bytes, string owner, string repository)
        {
            GitHubReleaseClient channel = new GitHubReleaseClient(owner, repository);
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumMetadataBytes) throw new InvalidDataException("Release metadata is empty or too large.");
            ReleaseRecord release;
            using (MemoryStream stream = new MemoryStream(bytes, false))
                release = (ReleaseRecord)new DataContractJsonSerializer(typeof(ReleaseRecord)).ReadObject(stream);
            Version version;
            if (release == null || release.Draft || release.Prerelease || !TryVersion(release.Tag, out version) || release.Assets == null)
                throw new InvalidDataException("GitHub did not return a stable Holiday Timer release.");
            AssetRecord asset = null;
            foreach (AssetRecord item in release.Assets)
            {
                if (item == null || item.Name != AssetName || item.State != "uploaded") continue;
                if (asset != null) throw new InvalidDataException("The release contains duplicate installer files.");
                asset = item;
            }
            if (asset == null || asset.Size <= 0 || asset.Size > MaximumInstallerBytes || asset.Digest == null ||
                !Regex.IsMatch(asset.Digest, @"^sha256:[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("The release has no verifiable Holiday Timer installer.");
            Uri download;
            string path = "/" + owner + "/" + repository + "/releases/download/" + release.Tag + "/" + AssetName;
            if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out download) || !IsHttpsHost(download, "github.com") ||
                download.AbsolutePath != path || download.Query.Length != 0 || download.Fragment.Length != 0)
                throw new InvalidDataException("The installer is not from the configured GitHub release.");
            return new AvailableUpdate { Version = version, Tag = release.Tag, Sha256 = asset.Digest.Substring(7), Size = asset.Size,
                DownloadUrl = download, ReleaseUrl = new Uri("https://github.com/" + owner + "/" + repository + "/releases/tag/" + release.Tag) };
        }

        internal static bool TryVersion(string text, out Version version)
        {
            version = null;
            if (String.IsNullOrEmpty(text) || !Regex.IsMatch(text, @"^v?(?:0|[1-9][0-9]{0,4})\.(?:0|[1-9][0-9]{0,4})\.(?:0|[1-9][0-9]{0,4})$")) return false;
            string[] parts = text.TrimStart('v').Split('.'); int major, minor, patch;
            if (!Int32.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major) ||
                !Int32.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor) ||
                !Int32.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch) || major > 65535 || minor > 65535 || patch > 65535) return false;
            version = new Version(major, minor, patch); return true;
        }
        internal static bool IsNewer(Version candidate, Version installed)
        {
            if (candidate == null || installed == null) return false;
            return candidate.CompareTo(new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build))) > 0;
        }

        private static HttpWebRequest NewRequest(Uri address)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(address);
            request.Method = "GET"; request.UserAgent = "HolidayTimer/1.7 update-check";
            request.Timeout = request.ReadWriteTimeout = 12000; request.AllowAutoRedirect = false;
            return request;
        }
        internal static bool IsAllowedDownload(Uri address)
        {
            return IsHttpsHost(address, "github.com") || IsHttpsHost(address, "release-assets.githubusercontent.com") ||
                IsHttpsHost(address, "objects.githubusercontent.com") || IsHttpsHost(address, "github-releases.githubusercontent.com");
        }
        private static bool IsHttpsHost(Uri address, string host)
        { return address != null && address.IsAbsoluteUri && address.Scheme == Uri.UriSchemeHttps && address.Port == 443 && address.UserInfo.Length == 0 && String.Equals(address.Host, host, StringComparison.OrdinalIgnoreCase); }

        internal string Download(AvailableUpdate release, string directory)
        {
            if (release == null || !IsAllowedDownload(release.DownloadUrl)) throw new InvalidDataException("The update source is invalid.");
            Uri address = release.DownloadUrl;
            byte[] bytes = null;
            for (int redirects = 0; redirects < 5; redirects++)
            {
                if (!IsAllowedDownload(address)) throw new InvalidDataException("The update redirected to an untrusted address.");
                HttpWebRequest request = NewRequest(address);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                    {
                        Uri next;
                        if (!Uri.TryCreate(address, response.Headers["Location"], out next)) throw new InvalidDataException("The update redirect is invalid.");
                        address = next; continue;
                    }
                    if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > release.Size)
                        throw new InvalidDataException("The update download is incomplete or too large.");
                    using (Stream stream = response.GetResponseStream()) bytes = ReadBounded(stream, release.Size);
                    break;
                }
            }
            if (bytes == null) throw new InvalidDataException("The update redirected too many times.");
            VerifyBytes(bytes, release);
            return StoreVerified(bytes, release, directory);
        }

        internal static byte[] ReadBounded(Stream stream, long limit)
        {
            if (stream == null || limit <= 0 || limit > MaximumInstallerBytes) throw new InvalidDataException("The download size is invalid.");
            Stopwatch timer = Stopwatch.StartNew();
            using (MemoryStream copy = new MemoryStream())
            {
                byte[] buffer = new byte[8192]; int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (timer.Elapsed.TotalSeconds > 60 || copy.Length + count > limit) throw new InvalidDataException("The download took too long or exceeded its expected size.");
                    copy.Write(buffer, 0, count);
                }
                return copy.ToArray();
            }
        }

        internal static void VerifyBytes(byte[] bytes, AvailableUpdate release)
        {
            if (bytes == null || release == null || bytes.LongLength != release.Size) throw new InvalidDataException("The installer download is incomplete.");
            string digest;
            using (SHA256 hash = SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
            if (!String.Equals(digest, release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The installer did not pass its SHA-256 check.");
        }

        internal static string StoreVerified(byte[] bytes, AvailableUpdate release, string directory)
        {
            VerifyBytes(bytes, release);
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("An update cache directory is required.");
            Directory.CreateDirectory(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("The update cache cannot be a link.");
            string target = Path.Combine(directory, AssetName);
            string temporary = Path.Combine(directory, "update-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                Version fileVersion;
                string raw = FileVersionInfo.GetVersionInfo(temporary).FileVersion;
                if (!TryFileVersion(raw, out fileVersion) || fileVersion != release.Version)
                    throw new InvalidDataException("The installer version does not match the GitHub release.");
                if (File.Exists(target))
                {
                    if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new IOException("The cached installer cannot be a link.");
                    File.Replace(temporary, target, null, true);
                }
                else File.Move(temporary, target);
                return target;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static void VerifyStoredInstaller(string path, AvailableUpdate release)
        {
            if (release == null || String.IsNullOrEmpty(path) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The cached installer is invalid.");
            byte[] bytes;
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length != release.Size) throw new InvalidDataException("The cached installer is incomplete.");
                bytes = ReadBounded(file, release.Size);
            }
            VerifyBytes(bytes, release);
            Version version;
            if (!TryFileVersion(FileVersionInfo.GetVersionInfo(path).FileVersion, out version) || version != release.Version)
                throw new InvalidDataException("The cached installer version does not match the release.");
        }
        internal static bool TryFileVersion(string text, out Version version)
        {
            version = null; Version value;
            if (String.IsNullOrWhiteSpace(text) || !Version.TryParse(text.Trim(), out value) || value.Build < 0) return false;
            version = new Version(value.Major, value.Minor, value.Build); return true;
        }
    }
}
