using VRCFaceTracking.Core.Params.Expressions;

namespace PicoFacialDataModule
{
    /// <summary>
    /// Applies the optional per-channel gain from <see cref="ModuleSettings.ShapeGain"/> to every
    /// ARKit blendshape this module maps, mirroring the reference Pico4SAFT module's configurable
    /// scales. Off by default (raw is returned untouched). When on, the value is multiplied by the
    /// channel's gain and clamped so it stays a valid weight: a gain below 1 attenuates, above 1
    /// amplifies, and a gain of exactly 0 (or a non-positive result) yields 0.
    /// </summary>
    public sealed class ShapeGain
    {
        /// <summary>Upper clamp for a scaled weight; matches the reference module's limiter.</summary>
        public const float MaxValue = 0.99f;

        private readonly bool enabled;
        private readonly float defaultGain;
        private readonly Dictionary<string, float> gains;

        public ShapeGain(ModuleSettings settings)
        {
            ShapeGainSettings config = settings.ShapeGain ?? new ShapeGainSettings();
            enabled = config.Enabled;
            defaultGain = config.DefaultGain;
            gains = new Dictionary<string, float>(config.Gains ?? new(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>True when the gain stage is enabled (used for the startup log line).</summary>
        public bool Active => enabled;

        /// <summary>Applies the channel's gain to a raw blendshape weight; returns <paramref name="raw"/> when off.</summary>
        public float Apply(UnifiedExpressions shape, float raw)
        {
            if (!enabled)
                return raw;

            float gain = GainFor(shape);
            if (gain == 1.0f)
                return raw;

            float scaled = raw * gain;
            return scaled < 0f ? 0f : scaled > MaxValue ? MaxValue : scaled;
        }

        /// <summary>Effective gain for one channel, from the override table or the default.</summary>
        public float GainFor(UnifiedExpressions shape)
        {
            return gains.TryGetValue(shape.ToString(), out float gain) ? gain : defaultGain;
        }
    }
}
