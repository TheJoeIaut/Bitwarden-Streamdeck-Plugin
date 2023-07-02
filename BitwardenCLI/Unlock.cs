using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRaider.SdTools;
using CliWrap;
using CliWrap.Buffered;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitwardenStreamdeckPlugin
{
    [PluginActionId("com.thejoeiaut.bitwardenunlock")]
    public class Unlock : PluginBase
    {
        private class PluginSettings
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

        private PluginSettings settings;

        #endregion

        public Unlock(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
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
            try
            {
                UnlockVault(settings.MasterPassword, settings.PasswordEnvVariable, settings.PasswordFile).GetAwaiter()
                    .GetResult();
                Connection.ShowOk();
            }
            catch (Exception e)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, e.Message);
                Connection.ShowAlert();
            }

            Connection.ShowOk();
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

        private static async Task UnlockVault(string masterPassword, string envVariable, string fileName)
        {
           
                Command cmd;

                if (!string.IsNullOrEmpty(masterPassword))
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "Unlock using Master Password");
                    cmd = BwCliWrapper.GetCli().WithArguments(new[] {"unlock", masterPassword, "--raw"});
                }
                else if (!string.IsNullOrEmpty(envVariable))
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "Unlock using Environment Variable");
                    cmd = BwCliWrapper.GetCli().WithArguments(new[] {"unlock", "--passwordenv", envVariable, "--raw"});
                }
                else if (!string.IsNullOrEmpty(fileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "Unlock using Password File");
                    cmd = BwCliWrapper.GetCli().WithArguments(new[] {"unlock", "--passwordfile", fileName, "--raw"});
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "No Settings Found - Doing nothing");
                    return;
                }

                var result = await cmd.ExecuteBufferedAsync();

                Logger.Instance.LogMessage(TracingLevel.INFO, "Session Key received");

                BwCliWrapper.SetCli(BwCliWrapper.GetCli().WithEnvironmentVariables(new Dictionary<string, string>
                {
                    ["BW_SESSION"] = result.StandardOutput
                }));

                foreach (var variable in BwCliWrapper.GetCli().EnvironmentVariables)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"{variable.Key}-{variable.Value}");
                }

                var response = await BwCliWrapper.GetCli().WithArguments(new[] {"status"}).ExecuteBufferedAsync();

                Logger.Instance.LogMessage(TracingLevel.INFO, response.StandardOutput);
                Logger.Instance.LogMessage(TracingLevel.INFO, "Session Key stored in Environment Variable");

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