using System.Text;

namespace PicoFacialDataModule
{
    /// <summary>
    /// Immutable description of the mode advertised by the headset-side bridge on a
    /// separate ASCII control datagram (see <see cref="Prefix"/>).
    ///
    /// This type is deliberately dependency-free so that it can be parsed and tested in isolation.
    /// </summary>
    public sealed class BridgeMode
    {
        /// <summary>ASCII prefix every mode control datagram starts with.</summary>
        public const string Prefix = "PXR_MODE";

        /// <summary>Whether the headset reports a rooted environment (rooted=1).</summary>
        public bool Rooted { get; }

        /// <summary>Whether the enhancement module reports itself as active (enhance=1).</summary>
        public bool EnhanceModule { get; }

        /// <summary>Whether the local per-eye gate is open (gate=1).</summary>
        public bool GateOn { get; }

        /// <summary>
        /// Effective enhancement state. <c>mode=enhance</c> takes precedence; when <c>mode</c>
        /// is absent this is true only when both <c>gate=1</c> and <c>enhance=1</c>.
        /// </summary>
        public bool Enhance { get; }

        /// <summary>Advertised plugin state: off, left, right or dual. Defaults to "off".</summary>
        public string Plugin { get; }

        /// <summary>Creates a fully specified instance.</summary>
        public BridgeMode(bool rooted, bool enhanceModule, bool gateOn, bool enhance, string plugin)
        {
            Rooted = rooted;
            EnhanceModule = enhanceModule;
            GateOn = gateOn;
            Enhance = enhance;
            Plugin = plugin ?? "off";
        }

        /// <summary>Creates a default (unadvertised / normal) instance.</summary>
        public BridgeMode() : this(false, false, false, false, "off")
        {
        }

        /// <summary>A default instance, used until (and unless) the bridge advertises a mode.</summary>
        public static readonly BridgeMode Normal = new BridgeMode();

        /// <summary>
        /// Tries to parse a control datagram. Returns false when <paramref name="datagram"/> is
        /// null or does not start with <see cref="Prefix"/>. Unknown keys and malformed tokens are
        /// ignored. Never throws.
        /// </summary>
        public static bool TryParse(byte[]? datagram, out BridgeMode mode)
        {
            mode = Normal;

            if (datagram == null || datagram.Length == 0)
                return false;

            string text;
            try
            {
                text = Encoding.ASCII.GetString(datagram);
            }
            catch
            {
                return false;
            }

            if (!text.StartsWith(Prefix, StringComparison.Ordinal))
                return false;

            bool rooted = false;
            bool enhanceModule = false;
            bool gateOn = false;
            string? advertisedMode = null;
            string plugin = "off";

            foreach (var token in text.Substring(Prefix.Length).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = token.IndexOf('=');

                if (separator <= 0 || separator == token.Length - 1)
                    continue;

                string key = token.Substring(0, separator);
                string value = token.Substring(separator + 1);

                switch (key)
                {
                    case "mode":
                        advertisedMode = value;
                        break;
                    case "rooted":
                        rooted = IsTrue(value);
                        break;
                    case "enhance":
                        enhanceModule = IsTrue(value);
                        break;
                    case "gate":
                        gateOn = IsTrue(value);
                        break;
                    case "plugin":
                        plugin = value;
                        break;
                    default:
                        break; // Unknown keys are ignored.
                }
            }

            bool enhance = advertisedMode != null
                ? string.Equals(advertisedMode, "enhance", StringComparison.OrdinalIgnoreCase)
                : gateOn && enhanceModule;

            mode = new BridgeMode(rooted, enhanceModule, gateOn, enhance, plugin);
            return true;
        }

        private static bool IsTrue(string value)
        {
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return $"enhance={Enhance} plugin={Plugin} rooted={Rooted} gate={GateOn} enhanceModule={EnhanceModule}";
        }
    }
}
