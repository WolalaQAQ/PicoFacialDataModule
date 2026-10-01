# PicoFacialDataModule

A VRCFaceTracking module that connects to the headset-side bridge over UDP, using the fork's split wire format.

> A fork of [thoricelli/PicoFacialDataModule](https://github.com/thoricelli/PicoFacialDataModule) (MIT), matched to the split-protocol [PicoFacialBridge](https://github.com/WolalaQAQ/PicoFacialBridge). It replaces the upstream module for that bridge and is **not** compatible with the upstream `picofacialdatadaemon` or an upstream module release.

## Running

1. Download the module ZIP from this fork's [Releases](https://github.com/WolalaQAQ/PicoFacialDataModule/releases), or build it from source (see below). Use this fork build — an upstream release does not speak the split protocol.
2. In VRCFaceTracking go to Module Registry > Press the plus at the top.
3. Select the ZIP you downloaded.
4. Install and run the matching **PicoFacialBridge** split-protocol APK on the headset. Do not use the old `picofacialdatadaemon` or an upstream module ZIP with this fork. Root/Magisk is optional for enhanced capabilities, not required for normal tracking.

Build locally from this fork root with `dotnet build PicoFacialDataModule/PicoFacialDataModule.csproj -c Release`, then ZIP `PicoFacialDataModule/bin/Release/net10.0/PicoFacialDataModule.dll` and `module.json` at the ZIP root. Never use Debug for verification: its post-build target deploys into the live VRCFT folder.

Offline regression tests: `dotnet run --project tests/Regression.csproj -c Release` (no device, sockets or user settings).

### Automatic mode detection

The headset-side bridge advertises the mode it is running in on a separate control datagram, and the
module picks it up automatically — no configuration is needed:

| Advertised mode | Eye tracking behaviour |
| --------------- | ---------------------- |
| Normal          | Fused (combined) gaze for both eyes, plus the real eye openness reported by the bridge. |
| Enhanced        | Real per-eye gaze and real per-eye pupil diameter, each used only for the eye that is currently gated open. The pupil gate here is the vendor's pupil-validity bit (`0x800`), so `eyePupilDilationEyeOpennessThreshold` does not apply. |

Enhanced mode is advertised by the headset bridge when an enhancement module is active. The wire format
is fork-only: this module and the fork bridge are updated together, and the stock 536-byte
`picofacialdatadaemon` framing is no longer spoken. Eye and facial updates arrive as separate tagged
datagrams at their own rates, so the module keeps the latest facial frame to resolve the eye parser's
blink fallback, EyeWide, EyeSquint and brow blendshapes. That cache is accepted within 250 ms of the eye sample's source timestamp and cleared on reconnect; old facial validity never gates fresh gaze/openness/pupil data. Invalid/stale facial samples stop updating derived eye shapes without forcing a neutral pose. Eye-only Bridge transmission still sends a low-rate F containing just eye shapes, with mouth data and face validity cleared.

### Settings

Settings can be found at: `%appdata%/VRCFaceTracking/CustomLibs/61ee1324-fd45-42f1-9636-8e28717cf6db/`.


| Setting                              | Description                                                                                                                                                                                                            | Default value |
| ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------- |
| DisableEyeTracking                   | Disables eye tracking in the module only.                                                                                                                                                                              | `false`       |
| DisableFaceTracking                  | Disables face tracking in the module only.                                                                                                                                                                             | `false`       |
|IP| Specifies the IP address of the PICO headset on your network (or outside of it).<br>Skips the multicast discovery process if set.| `null`
| eyePupilDilationEyeOpennessThreshold | This threshold will stop eye dilation tracking below a certain eye openness. Only used in normal mode; enhanced mode trusts the vendor's pupil-validity bit. | `0.8`         |

### Shape gain (optional)

The module can scale the ARKit blendshapes it maps, mirroring the reference Pico4SAFT module's
configurable scales. It is **off by default**, so tracking is unchanged until you turn it on. Edit
`PicoFacialDataModule.json` (next to the DLL) and restart VRCFaceTracking.

`Gains` already lists every channel this module maps, all at `1.00` — the same key list as the
reference module's `PicoModuleConfig.json` `"scales"` (minus its gaze/openness entries, which are not
shapes and are never scaled here). Tune the channels you care about in place:

```jsonc
"ShapeGain": {
  "Enabled": true,        // master switch
  "DefaultGain": 1.0,     // applied to every mapped channel without an entry below
  "Gains": {              // shipped full list, keyed by UnifiedExpressions name
    "EyeWideLeft": 1.0,
    "EyeWideRight": 1.0,
    "EyeSquintLeft": 1.0,
    "EyeSquintRight": 1.0,
    // ... every other mapped channel, all 1.0
  }
}
```

Every mapped shape becomes `clamp(raw * gain, 0, 0.99)`: a gain below 1 attenuates, above 1
amplifies, and the clamp keeps it a valid weight (so gains above 1 saturate at 0.99). Channel names
are case-insensitive; an unknown name is ignored. Only blendshape-derived shapes are scaled — gaze,
eye openness and pupil diameter come from the eye data and are not affected. When the switch is on the
log prints the effective eye-shape gains at startup.

### Babble

For those using a Babble face tracker, you can disable the face tracking for this module:

1. In explorer go to:

```shell
%appdata%/VRCFaceTracking/CustomLibs/61ee1324-fd45-42f1-9636-8e28717cf6db/
```

2. Open `PicoFacialDataModule.json` with a text editor of choice.
3. Disable face tracking, like so:

```shell
{
  "DisableEyeTracking": false,
  "DisableFaceTracking": true
}
```
