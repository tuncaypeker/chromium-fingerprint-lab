# Chromium Fingerprint Lab

Source patches and a local demo for studying how a Chromium build exposes browser fingerprint signals. The demo runs Chromium headlessly against a bundled HTML page and compares the baseline with readings from a JSON profile.

The patch targets Chromium `144.0.7537.0` at commit [`a09f50d4b209276b727a34de3d6950efb11a4e6b`](https://chromium.googlesource.com/chromium/src/+/a09f50d4b209276b727a34de3d6950efb11a4e6b). It is intended for controlled experiments with local or authorized test pages. Use a current, supported browser for ordinary browsing.

## Fingerprint signals you can override

Place a JSON object named `fingerprint-local.json` at the root of the directory passed to Chromium with `--user-data-dir`. The browser loads the settings on first use in each process. Restart the browser to apply edits. Use a separate user data directory for each experimental profile. Unset fields normally follow Chromium's default behavior on their respective surfaces.

### Browser identity, hardware and locale

| Setting | Surface and behavior |
| --- | --- |
| `userAgent` | Replaces Chromium's User-Agent string with a valid HTTP header value. It affects the regular `User-Agent` header and the user agent exposed through `navigator.userAgent`. |
| `uach` | Overrides parts of `navigator.userAgentData` and outgoing `Sec-CH-UA-*` headers. Supported fields include `brandVersionList`, `fullVersionList`, `uaFullVersion`, `platform`, `platformVersion`, `architecture`, `bitness`, `model`, `mobile`, and `wow64`. Brand lists contain `{ "brand": "…", "version": "…" }` objects. Use JSON booleans for `mobile` and `wow64`. Chromium's rules still govern which hints a request sends. |
| `navigatorPlatform` | Sets the string returned by `navigator.platform` in the shared window and worker implementation. Set `uach.platform` separately for the Client Hints platform. |
| `hardwareConcurrency` | Reports a positive integer through `navigator.hardwareConcurrency`. The value describes logical processors as seen by scripts. |
| `deviceMemory` | Overrides the approximate device-memory value with a positive integer in GiB where Chromium exposes it. |
| `maxTouchPoints` | Sets `navigator.maxTouchPoints`. Use `0` for no reported touch points. |
| `locale` | Supplies the preferred language list for `navigator.language`, `navigator.languages` and the HTTP `Accept-Language` header. Use a comma-separated list such as `en-GB,en`. |
| `timezone` | Sets ICU's default time zone using an IANA identifier such as `Europe/London`. This affects browser date and time formatting, including the zone reported by `Intl.DateTimeFormat`. While the profile supplies a zone, the patch also suppresses host time-zone updates to Chromium's ICU state. |

### Rendering, audio and timing

| Setting | Surface and behavior |
| --- | --- |
| `canvasSeed` | A nonzero integer changes RGB bytes in the canvas snapshot encoded by `HTMLCanvasElement.toDataURL()`. The override is in the data URL readback path. |
| `clientRectSeed` | A nonempty string adds small deterministic offsets to `x`, `y`, `width`, and `height` for rectangles returned through Blink's `DOMRect::FromRectF` path. |
| `audioSeed` | A nonzero integer selects a deterministic sample offset on the patched `OfflineAudioContext` rendering path when the rendered buffer is copied. |
| `timePrecisionMs` | A positive number rounds patched high-resolution timestamps down to multiples of the configured interval in milliseconds. |
| `timingNoiseMaxMs`, `profileSeed` | A positive `timingNoiseMaxMs` enables a deterministic offset on timestamps produced by the patched conversion. An optional `profileSeed` string determines the offset. When rounding is enabled, the offset is capped at 49% of the rounding interval. |

### WebGL and WebGPU

| Setting | Surface and behavior |
| --- | --- |
| `gpuVendor`, `gpuRenderer` | Override the unmasked vendor and renderer strings returned when `WEBGL_debug_renderer_info` is enabled. These are reported strings. |
| `webglVersion`, `webglShadingLanguageVersion` | Override the `VERSION` and `SHADING_LANGUAGE_VERSION` strings returned by WebGL and WebGL 2. |
| `gpuRenderBufferLimit`, `gpuTextureUnits`, `gpuTextureLimit` | Positive integers override the reported `MAX_RENDERBUFFER_SIZE`, `MAX_TEXTURE_IMAGE_UNITS`, and `MAX_TEXTURE_SIZE` limits. Actual graphics capabilities depend on the underlying driver. |
| `webglSeed` | A nonzero integer perturbs selected `readPixels()` results with supported formats and `UNSIGNED_BYTE` data, and changes the reported shader precision. Supplying the seed also adds a `#w` fragment containing its value to a WebGL canvas's data URL. |
| `webgpuVendor`, `webgpuArchitecture`, `webgpuDevice`, `webgpuDescription` | Override the corresponding fields on WebGPU adapter info when WebGPU is available. |

### Permission status and WebRTC

| Setting | Surface and behavior |
| --- | --- |
| `permissions` | Overrides Chromium's reported permission status for `geolocation`, `notifications`, `camera`, `microphone`, `sensors`, and `backgroundSync`. Set each entry to `granted`, `denied`, `prompt`, or `native` to defer to Chromium. Use status overrides in an isolated test profile. |
| `webRtcPolicy` | `MaskHostCandidates` hides selected host, server-reflexive and peer-reflexive ICE candidate fields from page scripts and filters their lines from the exposed SDP. The override operates on JavaScript-visible data. Network traffic and relay candidates follow Chromium's normal transport behavior. |

### Example profile

```json
{
  "navigatorPlatform": "Win32",
  "hardwareConcurrency": 8,
  "deviceMemory": 4,
  "maxTouchPoints": 0,
  "locale": "en-GB,en",
  "timezone": "Europe/London",
  "canvasSeed": 17,
  "clientRectSeed": "example-profile",
  "timePrecisionMs": 1,
  "timingNoiseMaxMs": 0.2,
  "profileSeed": "example-profile",
  "permissions": { "geolocation": "prompt", "camera": "denied" },
  "webRtcPolicy": "MaskHostCandidates"
}
```

The profile sets individual signals. To model a specific device in an experiment, choose consistent values for the User-Agent string, Client Hints, navigator and hardware fields. The patch also suppresses some Chromium startup infobars, including warnings about unsafe flags. Use these builds with test profiles and pages you control.

## Build the patched Chromium on Windows

Set up `depot_tools`, Git, Visual Studio, and the Windows SDK using the [Chromium Windows build guide for the pinned revision](https://chromium.googlesource.com/chromium/src/+/a09f50d4b209276b727a34de3d6950efb11a4e6b/docs/windows_build_instructions.md). Clone this repository and save its path in `$lab`.

```powershell
git clone https://github.com/tuncaypeker/chromium-fingerprint-lab.git
$lab = (Resolve-Path .\chromium-fingerprint-lab).Path
```

In a separate empty folder, fetch Chromium and switch to the pinned tag. If you already have a Chromium checkout at this revision with synced dependencies, start with `git apply --check` in its `src` directory.

```powershell
fetch --nohooks chromium
Set-Location src
git fetch origin tag 144.0.7537.0
git checkout --detach refs/tags/144.0.7537.0
gclient sync -D --revision src@a09f50d4b209276b727a34de3d6950efb11a4e6b
git rev-parse HEAD

$patch = Join-Path $lab 'patches/chromium-144.0.7537.0.patch'
git apply --check --whitespace=error-all $patch
git apply $patch

gn gen out/FingerprintLab --args='is_debug=false'
autoninja -C out/FingerprintLab chrome
```

The build produces `out/FingerprintLab/chrome.exe`. Keep it with the other files in the output directory when running the demo.

## Run the local demo

Install the .NET 8 SDK or a newer SDK with .NET 8 targeting and runtime support. Run this from the Chromium Fingerprint Lab directory and pass the path to your patched executable.

```powershell
dotnet run --project demo -- 'C:\chromium\src\out\FingerprintLab\chrome.exe'
```

The example profile sets `navigator.hardwareConcurrency` to `3`, `navigator.maxTouchPoints` to `5`, and `navigator.platform` to `Linux x86_64`. The demo uses Playwright with the supplied executable and creates a temporary user data directory for each run. It loads the bundled HTML in an offline browser context, prints the baseline and configured readings, and returns exit code `1` if the configured readings differ from the profile.

To try other values, copy [the demo profile](demo/profile.example.json), edit the three fields, and pass the path as a second argument after the executable. The demo writes `fingerprint-local.json` automatically. Additional profile settings are passed to Chromium, while the demo compares only the three navigator readings.

The patch was checked against the pinned Chromium sources. Build Chromium with the current patch before running the demo so that the browser and demo use the same profile filename.

## License

Project contributions are available under [BSD-3-Clause](LICENSE). Chromium and Blink code in the patch retains its original terms. See the [third-party notices](licenses/README.md).
