using System;
using System.Globalization;
using System.Threading.Tasks;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Pulls the latest vault data down from the server with 'bw sync'.
    ///
    /// The CLI keeps its own local copy of the vault and only refreshes it when asked, so
    /// an entry added on another device - or a password changed in the web vault - stays
    /// invisible to the Get action until a sync has run. This needs an unlocked vault:
    /// 'bw sync' cannot decrypt anything without a session key.
    /// </summary>
    [PluginActionId("com.thejoeiaut.bitwardensync")]
    public class Sync : KeypadBase
    {
        internal class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                return new PluginSettings
                {
                    Force = false,
                    ShowLastSync = false
                };
            }

            /// <summary>
            /// Throws the local copy away and downloads the whole vault again
            /// ('bw sync --force') instead of the usual incremental sync.
            /// </summary>
            [JsonProperty(PropertyName = "force")]
            public bool Force { get; set; }

            /// <summary>
            /// Puts the time of the last successful sync on the key.
            /// </summary>
            [JsonProperty(PropertyName = "showlastsync")]
            public bool ShowLastSync { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private readonly IBwCli cli;

        #endregion

        public Sync(ISDConnection connection, InitialPayload payload)
            : this(connection, payload, BwCli.Shared)
        {
        }

        internal Sync(ISDConnection connection, InitialPayload payload, IBwCli cli) : base(connection, payload)
        {
            this.cli = cli;

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
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed - Sync");

            // A sync talks to the server, so it takes as long as the network does. Unlike
            // lock, it must not be waited on inside the Stream Deck event loop. Failures
            // are reported from inside SyncVault.
            Task.Run(SyncVault);
        }

        internal async Task SyncVault()
        {
            try
            {
                await cli.Run(BuildSyncArguments(settings));

                if (settings.ShowLastSync)
                {
                    await RefreshLastSyncTitle();
                }

                await Connection.ShowOk();
            }
            catch (Exception ex)
            {
                // A locked vault is the usual cause: 'bw sync' has nothing to decrypt with.
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);
                await Connection.ShowAlert();
            }
        }

        /// <summary>
        /// Translates the action's settings into 'bw sync' arguments.
        /// </summary>
        internal static string[] BuildSyncArguments(PluginSettings settings)
        {
            if (settings.Force)
            {
                return new[] { "sync", "--force" };
            }

            return new[] { "sync" };
        }

        /// <summary>
        /// Writes the time of the last successful sync onto the key.
        ///
        /// This is deliberately not fatal: the sync itself has already succeeded by the
        /// time it runs, so an unreadable or unparsable timestamp costs the title, not the
        /// press.
        /// </summary>
        internal async Task RefreshLastSyncTitle()
        {
            try
            {
                string title = FormatLastSync(await cli.Run("sync", "--last"));

                if (title != null)
                {
                    await Connection.SetTitleAsync(title);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not read the last sync time: {ex.Message}");
            }
        }

        /// <summary>
        /// Turns the ISO 8601 UTC timestamp 'bw sync --last' prints into a key title.
        ///
        /// A Stream Deck key has room for very little text, so this is the local time of
        /// day only - the date is what a glance at the key is least likely to be about.
        /// Returns null when the CLI printed something unexpected, or nothing at all,
        /// which is what a vault that has never been synced produces.
        /// </summary>
        internal static string FormatLastSync(string commandOutput)
        {
            string value = commandOutput?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset synced))
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Unexpected 'bw sync --last' output: {value}");
                return null;
            }

            return synced.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
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

            // A title left over from when the option was on would otherwise sit on the key
            // forever, ageing silently.
            if (!settings.ShowLastSync)
            {
                Connection.SetTitleAsync(null);
            }

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
