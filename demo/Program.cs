using System.Text.Json;
using Microsoft.Playwright;

const string profileFileName = "fingerprint-local.json";
const string usage = "Usage: dotnet run --project demo -- <path-to-Chromium> [profile.json]";
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

if (args is ["--help"] or ["-h"])
{
    Console.WriteLine(usage);
    return 0;
}

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine(usage);
    return 2;
}

try
{
    var executable = Path.GetFullPath(args[0]);
    if (!File.Exists(executable))
        throw new FileNotFoundException($"Chromium executable not found: {executable}");

    var profilePath = args.Length == 2
        ? args[1]
        : Path.Combine(AppContext.BaseDirectory, "profile.example.json");
    var profileJson = await File.ReadAllTextAsync(profilePath);
    var expected = ReadSignals(profileJson);
    var probeHtml = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "probe.html"));
    using var playwright = await Playwright.CreateAsync();

    var baseline = await ProbeAsync(playwright.Chromium, executable, probeHtml, null);
    var configured = await ProbeAsync(playwright.Chromium, executable, probeHtml, profileJson);

    Console.WriteLine($"Baseline: {JsonSerializer.Serialize(baseline, jsonOptions)}");
    Console.WriteLine($"Configured: {JsonSerializer.Serialize(configured, jsonOptions)}");
    Console.WriteLine($"Expected: {JsonSerializer.Serialize(expected, jsonOptions)}");

    if (configured != expected)
    {
        Console.Error.WriteLine("The configured readings differ from the profile. Check that the executable includes the patches.");
        return 1;
    }

    Console.WriteLine(baseline == configured
        ? "The readings match the profile, and the baseline already reported the same values."
        : "The readings match the profile and differ from the baseline.");
    return 0;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or
    JsonException or PlaywrightException or ArgumentException or TimeoutException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

Signals ReadSignals(string json)
{
    var signals = JsonSerializer.Deserialize<Signals>(json, jsonOptions);
    if (signals is null || string.IsNullOrWhiteSpace(signals.NavigatorPlatform) ||
        signals.HardwareConcurrency < 1 || signals.MaxTouchPoints < 0)
        throw new JsonException("Expected positive hardwareConcurrency, nonnegative maxTouchPoints, and a nonempty navigatorPlatform.");

    return signals;
}

async Task<Signals> ProbeAsync(IBrowserType chromium, string executable, string probeHtml, string? profileJson)
{
    var profileDirectory = Directory.CreateTempSubdirectory("fingerprint-lab-");
    try
    {
        if (profileJson is not null)
            await File.WriteAllTextAsync(Path.Combine(profileDirectory.FullName, profileFileName), profileJson);

        await using var browser = await chromium.LaunchPersistentContextAsync(profileDirectory.FullName, new()
        {
            ExecutablePath = executable,
            Headless = true,
            ChromiumSandbox = true,
            Offline = true,
            Timeout = 15_000
        });
        browser.SetDefaultTimeout(15_000);
        var page = browser.Pages.First();
        await page.SetContentAsync(probeHtml);
        return ReadSignals(await page.Locator("#fingerprint").InnerTextAsync());
    }
    finally
    {
        try
        {
            profileDirectory.Delete(recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not remove temporary Chromium profile: {profileDirectory.FullName}");
        }
    }
}

sealed record Signals
{
    public required int HardwareConcurrency { get; init; }
    public required int MaxTouchPoints { get; init; }
    public required string NavigatorPlatform { get; init; }
}
