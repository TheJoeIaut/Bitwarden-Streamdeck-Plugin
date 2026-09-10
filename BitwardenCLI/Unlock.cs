using System;
using System.Threading.Tasks;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitwardenStreamdeckPlugin
{
    [PluginActionId("com.thejoeiaut.bitwardenunlock")]
    public class Unlock : KeypadBase
    {
        internal class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                var instance = new PluginSettings
                {
                    MasterPassword = string.Empty,
                    PasswordEnvVariable = string.Empty,
                    PasswordFile = string.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "masterpassword")]
            public string MasterPassword { get; set; }

            [JsonProperty(PropertyName = "passwordenvvar")]
            public string PasswordEnvVariable { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "passwordfile")]
            public string PasswordFile { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private readonly IBwCli cli;

        #endregion

        public Unlock(ISDConnection connection, InitialPayload payload)
            : this(connection, payload, BwCli.Shared)
        {
        }

        internal Unlock(ISDConnection connection, InitialPayload payload, IBwCli cli) : base(connection, payload)
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
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed - Unlock");
            UnlockVault().GetAwaiter().GetResult();
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

        /// <summary>
        /// Builds the 'bw unlock' arguments for the configured credential source, or null
        /// when nothing is configured.
        /// </summary>
        internal static string[] BuildUnlockArguments(PluginSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.MasterPassword))
            {
                return new[] { "unlock", settings.MasterPassword, "--raw" };
            }

            if (!string.IsNullOrEmpty(settings.PasswordEnvVariable))
            {
                return new[] { "unlock", "--passwordenv", settings.PasswordEnvVariable, "--raw" };
            }

            if (!string.IsNullOrEmpty(settings.PasswordFile))
            {
                return new[] { "unlock", "--passwordfile", settings.PasswordFile, "--raw" };
            }

            return null;
        }

        internal async Task UnlockVault()
        {
            string[] arguments = BuildUnlockArguments(settings);

            if (arguments == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "No unlock method configured - doing nothing");
                await Connection.ShowAlert();
                return;
            }

            try
            {
                string sessionKey = (await cli.Run(arguments)).Trim();

                if (string.IsNullOrEmpty(sessionKey))
                {
                    throw new InvalidOperationException("Bitwarden CLI returned an empty session key");
                }

                cli.SetSessionKey(sessionKey);
                Logger.Instance.LogMessage(TracingLevel.INFO, "Session key received and stored");

                await Connection.ShowOk();
            }
            catch (Exception e)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, e.Message);
                await Connection.ShowAlert();
            }
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
