using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Generates a password or passphrase with the Bitwarden generator and types it at the
    /// cursor. 'bw generate' works on a locked vault - it needs no login at all - so this
    /// action is usable without unlocking first.
    /// </summary>
    [PluginActionId("com.thejoeiaut.bitwardengenerate")]
    public class Generate : KeypadBase
    {
        /// <summary>
        /// The Bitwarden CLI refuses shorter values than these.
        /// </summary>
        internal const int MinimumLength = 5;
        internal const int MinimumWords = 3;

        internal class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                // Mirrors the Bitwarden generator's own defaults.
                return new PluginSettings
                {
                    GeneratorType = "password",
                    Length = 14,
                    Uppercase = true,
                    Lowercase = true,
                    Number = true,
                    Special = false,
                    MinNumber = 1,
                    MinSpecial = 1,
                    AvoidAmbiguous = false,
                    Words = 3,
                    Separator = "-",
                    Capitalize = false,
                    IncludeNumber = false
                };
            }

            /// <summary>"password" or "passphrase".</summary>
            [JsonProperty(PropertyName = "generatortype")]
            public string GeneratorType { get; set; }

            [JsonProperty(PropertyName = "length")]
            public int Length { get; set; }

            [JsonProperty(PropertyName = "uppercase")]
            public bool Uppercase { get; set; }

            [JsonProperty(PropertyName = "lowercase")]
            public bool Lowercase { get; set; }

            [JsonProperty(PropertyName = "number")]
            public bool Number { get; set; }

            [JsonProperty(PropertyName = "special")]
            public bool Special { get; set; }

            [JsonProperty(PropertyName = "minnumber")]
            public int MinNumber { get; set; }

            [JsonProperty(PropertyName = "minspecial")]
            public int MinSpecial { get; set; }

            [JsonProperty(PropertyName = "avoidambiguous")]
            public bool AvoidAmbiguous { get; set; }

            [JsonProperty(PropertyName = "words")]
            public int Words { get; set; }

            [JsonProperty(PropertyName = "separator")]
            public string Separator { get; set; }

            [JsonProperty(PropertyName = "capitalize")]
            public bool Capitalize { get; set; }

            [JsonProperty(PropertyName = "includenumber")]
            public bool IncludeNumber { get; set; }

            internal bool IsPassphrase =>
                string.Equals(GeneratorType, "passphrase", StringComparison.OrdinalIgnoreCase);
        }

        #region Private Members

        private readonly PluginSettings settings;
        private readonly IBwCli cli;
        private readonly IKeyboardTyper typer;

        #endregion

        public Generate(ISDConnection connection, InitialPayload payload)
            : this(connection, payload, BwCli.Shared, KeyboardTyper.Shared)
        {
        }

        internal Generate(ISDConnection connection, InitialPayload payload, IBwCli cli, IKeyboardTyper typer)
            : base(connection, payload)
        {
            this.cli = cli;
            this.typer = typer;

            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed - Generate");

            // Typing must not block the Stream Deck event loop.
            Task.Run(TypeGeneratedSecret);
        }

        internal async Task TypeGeneratedSecret()
        {
            try
            {
                string secret = await GenerateSecret();
                await typer.TypeText(secret);
                await Connection.ShowOk();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);
                await Connection.ShowAlert();
            }
        }

        internal async Task<string> GenerateSecret()
        {
            string[] arguments = BuildGenerateArguments(settings);

            // Deliberately logs the options and never the result.
            Logger.Instance.LogMessage(TracingLevel.INFO,
                $"Generating with: {string.Join(" ", arguments)}");

            string generated = (await cli.Run(arguments)).Trim();

            if (string.IsNullOrEmpty(generated))
            {
                throw new InvalidOperationException("Bitwarden CLI returned an empty password");
            }

            return generated;
        }

        /// <summary>
        /// Translates the action's settings into 'bw generate' arguments.
        /// </summary>
        internal static string[] BuildGenerateArguments(PluginSettings settings)
        {
            var arguments = new List<string> { "generate" };

            if (settings.IsPassphrase)
            {
                arguments.Add("--passphrase");
                arguments.Add("--words");
                arguments.Add(AtLeast(settings.Words, MinimumWords, "words"));

                if (!string.IsNullOrEmpty(settings.Separator))
                {
                    // 'space' and 'empty' are keywords the CLI understands; anything else
                    // is taken literally.
                    arguments.Add("--separator");
                    arguments.Add(settings.Separator);
                }

                if (settings.Capitalize)
                {
                    arguments.Add("--capitalize");
                }

                if (settings.IncludeNumber)
                {
                    arguments.Add("--includeNumber");
                }

                return arguments.ToArray();
            }

            if (!settings.Uppercase && !settings.Lowercase && !settings.Number && !settings.Special)
            {
                // Falling back to a default here would hand the user a weaker password than
                // the one they think they configured, so refuse instead.
                throw new InvalidOperationException(
                    "Select at least one character type (uppercase, lowercase, number or special)");
            }

            if (settings.Uppercase)
            {
                arguments.Add("--uppercase");
            }

            if (settings.Lowercase)
            {
                arguments.Add("--lowercase");
            }

            if (settings.Number)
            {
                arguments.Add("--number");
            }

            if (settings.Special)
            {
                arguments.Add("--special");
            }

            arguments.Add("--length");
            arguments.Add(AtLeast(settings.Length, MinimumLength, "length"));

            // Both minimums are always stated, including as zero for a disabled class.
            // Left unset, the CLI falls back to the logged in account's saved generator
            // options, whose minimum of one forces digits or symbols into the password
            // even though the corresponding character type was never requested.
            arguments.Add("--minNumber");
            arguments.Add(Minimum(settings.Number, settings.MinNumber));

            arguments.Add("--minSpecial");
            arguments.Add(Minimum(settings.Special, settings.MinSpecial));

            if (settings.AvoidAmbiguous)
            {
                arguments.Add("--ambiguous");
            }

            return arguments.ToArray();
        }

        /// <summary>
        /// The minimum count for a character class: the configured value when the class is
        /// enabled, otherwise zero to keep it out of the result entirely.
        /// </summary>
        private static string Minimum(bool enabled, int configured)
        {
            int value = enabled ? Math.Max(0, configured) : 0;
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Raises a value to the CLI's minimum. An unset or too small value is otherwise
        /// rejected by 'bw generate' outright.
        /// </summary>
        private static string AtLeast(int value, int minimum, string name)
        {
            if (value < minimum)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN,
                    $"Configured {name} {value} is below the Bitwarden minimum; using {minimum}");
                value = minimum;
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        public override void KeyReleased(KeyPayload payload)
        {
        }

        public override void OnTick()
        {
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
        }

        #region Private Methods

        private void SaveSettings()
        {
            Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        #endregion
    }
}
