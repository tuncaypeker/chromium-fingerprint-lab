# Chromium Fingerprint Lab

Source patches and a local demo for studying how a Chromium build exposes browser fingerprint signals. The demo runs Chromium headlessly against a bundled HTML page and compares the baseline with readings from a JSON profile.

The patch targets Chromium `144.0.7537.0` at commit [`a09f50d4b209276b727a34de3d6950efb11a4e6b`](https://chromium.googlesource.com/chromium/src/+/a09f50d4b209276b727a34de3d6950efb11a4e6b). It includes changes for navigator, locale and time zone, Client Hints, canvas, audio, WebGL, WebGPU, and WebRTC. This Chromium revision is intended for controlled experiments with local or authorized test pages. Use a current, supported browser for ordinary browsing.

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

To try other values, copy [the example profile](demo/profile.example.json), edit the three fields, and pass the path as a second argument after the executable. The patched browser reads `weblm-fingerprint.json` from the user data directory before reporting the configured signals. The demo writes that file automatically.

The patch was checked against the pinned Chromium sources. The three demo readings were verified on Windows with an existing patched build of the same Chromium version.

## License

Project contributions are available under [BSD-3-Clause](LICENSE). Chromium and Blink code in the patch retains its original terms. See the [third-party notices](licenses/README.md).
