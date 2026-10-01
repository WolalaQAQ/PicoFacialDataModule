using System.Reflection;
using System.Text.Json;

namespace PicoFacialDataModule
{
    public class ModuleSettings {
        public bool DisableEyeTracking { get; set; }
        public bool DisableFaceTracking { get; set; }
        /// <summary>
        /// When defined, it will not send a multicast but instead connect directly.
        /// </summary>
        public string? IP { get; set; }
        public TrackingSettings TrackingSettings { get; set; } = new();
        public ShapeGainSettings ShapeGain { get; set; } = new();
    }

    public class TrackingSettings {
        public float eyePupilDilationEyeOpennessThreshold { get; set; } = 0.8f;
    }

    /// <summary>
    /// Optional per-channel gain for the ARKit blendshapes this module maps (EyeWide/EyeSquint,
    /// brows, mouth, ...). Disabled by default, so tracking is bit-for-bit unchanged until it is
    /// turned on. When enabled, every mapped shape is multiplied by its gain and clamped to
    /// [0, 0.99]. Keys in <see cref="Gains"/> are UnifiedExpressions names, case-insensitive
    /// (e.g. "EyeWideLeft", "EyeSquintRight"); a channel with no entry uses <see cref="DefaultGain"/>.
    /// <para>
    /// The shipped key list mirrors the reference Pico4SAFT module's PicoModuleConfig "scales"
    /// (which also lists every channel at 1.00), minus its gaze/openness entries: those come from
    /// the eye data, not from shapes, so this gain never touches them.
    /// </para>
    /// </summary>
    public class ShapeGainSettings {
        public bool Enabled { get; set; } = false;
        public float DefaultGain { get; set; } = 1.0f;
        public Dictionary<string, float> Gains { get; set; } = NeutralGains();

        /// <summary>Every channel this module maps, at the reference module's default 1.00.</summary>
        private static Dictionary<string, float> NeutralGains()
        {
            string[] channels =
            {
                "BrowInnerUpLeft", "BrowInnerUpRight", "BrowOuterUpLeft", "BrowOuterUpRight",
                "BrowLowererLeft", "BrowLowererRight", "BrowPinchLeft", "BrowPinchRight",
                "EyeSquintLeft", "EyeSquintRight", "EyeWideLeft", "EyeWideRight",
                "JawOpen", "JawLeft", "JawRight", "JawForward", "MouthClosed",
                "CheekPuffLeft", "CheekPuffRight", "CheekSquintLeft", "CheekSquintRight",
                "NoseSneerLeft", "NoseSneerRight",
                "MouthUpperUpLeft", "MouthUpperUpRight", "MouthUpperDeepenLeft", "MouthUpperDeepenRight",
                "MouthLowerDownLeft", "MouthLowerDownRight",
                "MouthFrownLeft", "MouthFrownRight", "MouthDimpleLeft", "MouthDimpleRight",
                "MouthUpperLeft", "MouthLowerLeft", "MouthUpperRight", "MouthLowerRight",
                "MouthPressLeft", "MouthPressRight", "MouthRaiserLower", "MouthRaiserUpper",
                "MouthCornerPullLeft", "MouthCornerSlantLeft", "MouthCornerPullRight", "MouthCornerSlantRight",
                "MouthStretchLeft", "MouthStretchRight",
                "LipFunnelUpperLeft", "LipFunnelUpperRight", "LipFunnelLowerLeft", "LipFunnelLowerRight",
                "LipPuckerUpperLeft", "LipPuckerUpperRight", "LipPuckerLowerLeft", "LipPuckerLowerRight",
                "LipSuckUpperLeft", "LipSuckUpperRight", "LipSuckLowerLeft", "LipSuckLowerRight",
                "TongueOut"
            };

            var gains = new Dictionary<string, float>(channels.Length, StringComparer.OrdinalIgnoreCase);
            foreach (string channel in channels)
                gains[channel] = 1.0f;
            return gains;
        }
    }

    public class DaemonSettings
    {
        //TODO.
    }

    public class SettingsManager
    {
        private const string configFileName = "PicoFacialDataModule.json";
        
        public static ModuleSettings GetOrCreate()
        {
            string configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, configFileName);

            if (!File.Exists(configPath))
            {
                ModuleSettings moduleSettings = new ModuleSettings();

                string defaultJson = JsonSerializer.Serialize(moduleSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, defaultJson);

                return moduleSettings;
            }

            var settings = JsonSerializer.Deserialize<ModuleSettings>(File.ReadAllText(configPath))!;

            // Re-save the settings, so that any new settings between versions are added to the file.
            File.WriteAllText(configPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));

            return settings;
        }
    }
}
