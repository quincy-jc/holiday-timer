using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HolidayTimer;

internal static class GitHubReleaseTests
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL: " + message); checks++; }
    private static void Reject(Action action, string message)
    { try { action(); } catch (Exception e) { if (e is ArgumentException || e is IOException || e is InvalidDataException || e is System.Runtime.Serialization.SerializationException) { checks++; return; } throw; } throw new Exception("FAIL: " + message); }
    private static string Hash(byte[] bytes) { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static string Json(string tag, string digest, long size, string url, string extraAssets = "", bool draft = false, bool prerelease = false)
    {
        return "{\"tag_name\":\"" + tag + "\",\"draft\":" + draft.ToString().ToLowerInvariant() + ",\"prerelease\":" + prerelease.ToString().ToLowerInvariant() +
            ",\"assets\":[{\"name\":\"HolidayTimerSetup.exe\",\"state\":\"uploaded\",\"size\":" + size + ",\"digest\":\"sha256:" + digest + "\",\"browser_download_url\":\"" + url + "\"}" + extraAssets + "]}";
    }
    private static AvailableUpdate Parse(string json) { return GitHubReleaseClient.ParseLatest(Encoding.UTF8.GetBytes(json), "quincy-jc", "holiday-timer"); }
    private static int Main(string[] args)
    {
        try
        {
            System.Net.HttpWebRequest metadataRequest = GitHubReleaseClient.CreateMetadataRequest(new Uri("https://api.github.com/repos/quincy-jc/holiday-timer/releases/latest"));
            Check(metadataRequest.Accept == "application/vnd.github+json" && metadataRequest.Headers["X-GitHub-Api-Version"] == "2022-11-28",
                "The GitHub request sets restricted Accept using its supported property without making a network call");
            byte[] sample = Encoding.UTF8.GetBytes("A small, synthetic update file");
            string digest = Hash(sample), url = "https://github.com/quincy-jc/holiday-timer/releases/download/v1.7.0/HolidayTimerSetup.exe";
            string json = Json("v1.7.0", digest, sample.Length, url);
            AvailableUpdate available = Parse(json);
            Check(available.Version == new Version(1, 7, 0) && available.Size == sample.Length && available.Sha256 == digest, "A complete stable GitHub installer is parsed");
            Check(GitHubReleaseClient.IsNewer(available.Version, new Version(1, 6, 0, 0)), "A newer release is offered");
            Check(!GitHubReleaseClient.IsNewer(available.Version, new Version(1, 7, 0, 0)) && !GitHubReleaseClient.IsNewer(available.Version, new Version(2, 0, 0, 0)), "Equal and older releases are not offered");
            Check(available.ReleaseUrl.AbsoluteUri == "https://github.com/quincy-jc/holiday-timer/releases/tag/v1.7.0", "Release notes stay in the configured repository");
            GitHubReleaseClient.VerifyBytes(sample, available); checks++;
            Reject(delegate { GitHubReleaseClient.VerifyBytes(Encoding.UTF8.GetBytes("not the update file"), available); }, "Corrupt or incomplete installer is rejected");
            byte[] altered = (byte[])sample.Clone(); altered[0] ^= 1;
            Reject(delegate { GitHubReleaseClient.VerifyBytes(altered, available); }, "Equal-length content with a wrong digest is rejected");
            Reject(delegate { Parse(Json("v1.7.0", digest, sample.Length, url, "", true)); }, "Draft release is ignored");
            Reject(delegate { Parse(Json("v1.7.0", digest, sample.Length, url, "", false, true)); }, "Prerelease is ignored");
            Reject(delegate { Parse(Json("v1.7.0-beta", digest, sample.Length, url)); }, "Prerelease version suffix is rejected");
            Reject(delegate { Parse(Json("v01.7.0", digest, sample.Length, url)); }, "Ambiguous leading-zero versions are rejected");
            Reject(delegate { Parse(Json("v1.7.0", "", sample.Length, url)); }, "Missing digest is rejected");
            Reject(delegate { Parse(Json("v1.7.0", digest, 0, url)); }, "Empty installer is rejected");
            Reject(delegate { Parse(Json("v1.7.0", digest, 70L * 1024 * 1024, url)); }, "Oversized installer is rejected");
            Reject(delegate { Parse(Json("v1.7.0", digest, sample.Length, url.Replace("https://", "http://"))); }, "Unencrypted installer URL is rejected");
            Reject(delegate { Parse(Json("v1.7.0", digest, sample.Length, url.Replace("quincy-jc", "another-owner"))); }, "Different owner is rejected");
            Reject(delegate { Parse(Json("v1.7.0", digest, sample.Length, url.Replace("v1.7.0", "v1.8.0"))); }, "Different tag is rejected");
            Reject(delegate { Parse(Json("v1.7.0", digest, sample.Length, url + "?unsafe=1")); }, "Unexpected original download query is rejected");
            Reject(delegate { Parse(Json("v1.7.0", digest, sample.Length, url, ", {\"name\":\"HolidayTimerSetup.exe\",\"state\":\"uploaded\"}")); }, "Duplicate installers are rejected");
            Reject(delegate { Parse("{\"tag_name\":\"v1.7.0\",\"assets\":[]}"); }, "Missing required release fields are rejected");
            Check(GitHubReleaseClient.IsAllowedDownload(new Uri("https://release-assets.githubusercontent.com/file?signature=x")), "GitHub's release asset CDN is allowed");
            Check(!GitHubReleaseClient.IsAllowedDownload(new Uri("https://github.com.attacker.example/file")) && !GitHubReleaseClient.IsAllowedDownload(new Uri("https://github.com:444/file")), "Lookalike hosts and unexpected ports are rejected");
            Reject(delegate { new GitHubReleaseClient("../other", "holiday-timer"); }, "Owner cannot change the API path");
            Reject(delegate { new GitHubReleaseClient("quincy-jc", "../other"); }, "Repository cannot change the API path");
            using (MemoryStream stream = new MemoryStream(sample))
                Reject(delegate { GitHubReleaseClient.ReadBounded(stream, sample.Length - 1); }, "Downloads cannot exceed the declared size");
            Version version;
            Check(GitHubReleaseClient.TryFileVersion("1.6.0.0 ", out version) && version == new Version(1, 6, 0), "Installer version normalises Windows revision and padding");
            Check(!GitHubReleaseClient.TryFileVersion("not-a-version", out version), "Invalid executable version is rejected");
            if (args.Length > 0)
            {
                byte[] actual = File.ReadAllBytes(args[0]); Version actualVersion;
                Check(GitHubReleaseClient.TryFileVersion(FileVersionInfo.GetVersionInfo(args[0]).FileVersion, out actualVersion), "A real built executable exposes its version");
                AvailableUpdate real = new AvailableUpdate { Version = actualVersion, Size = actual.Length, Sha256 = Hash(actual) };
                string scratch = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update-checks-" + Guid.NewGuid().ToString("N"));
                GitHubReleaseClient.VerifyBytes(actual, real);
                string stored = GitHubReleaseClient.StoreVerified(actual, real, scratch);
                Check(File.Exists(stored) && Hash(File.ReadAllBytes(stored)) == real.Sha256, "A verified executable is stored intact");
                GitHubReleaseClient.VerifyStoredInstaller(stored, real); checks++;
                byte[] corrupt = (byte[])actual.Clone(); corrupt[0] ^= 1;
                File.WriteAllBytes(stored, corrupt);
                Reject(delegate { GitHubReleaseClient.VerifyStoredInstaller(stored, real); }, "Cached installer is checked again before launch");
                File.WriteAllBytes(stored, actual);
                real.Version = new Version(actualVersion.Major, actualVersion.Minor, actualVersion.Build + 1);
                Reject(delegate { GitHubReleaseClient.StoreVerified(actual, real, scratch); }, "A version mismatch cannot replace the previous verified file");
                Check(Hash(File.ReadAllBytes(stored)) == real.Sha256 && Directory.GetFiles(scratch, "*.tmp").Length == 0, "A failed version check retains the prior file and removes its temporary file");
            }
            Console.WriteLine("PASS " + checks + " GitHub update validation checks. No network, installer launch or user settings were touched.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
