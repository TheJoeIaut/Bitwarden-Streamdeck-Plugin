using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BitwardenStreamdeckPlugin.Models;
using CliWrap.Buffered;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WindowsInput;
using WindowsInput.Native;

namespace BitwardenStreamdeckPlugin
{
    [PluginActionId("com.thejoeiaut.bitwardenget")]
    public class Get : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    ItemName = String.Empty,
                    SelectedItemInformation = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "iteminformation")]
            public string SelectedItemInformation { get; set; }


            [JsonProperty(PropertyName = "items")] public List<ItemListDto> Items { get; set; }


            [JsonProperty(PropertyName = "itemname")]
            public string ItemName { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;

        #endregion

        public Get(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            try
            {
                var item = GetItem().GetAwaiter().GetResult();

                Task.Run(() =>
                {

                    var iis = new InputSimulator();

                    Logger.Instance.LogMessage(TracingLevel.INFO, $"PW {item.Password}");

                    switch (settings.SelectedItemInformation)
                    {
                        case "password":
                            iis.Keyboard.TextEntry(item.Password);
                            break;
                        case "username":
                            iis.Keyboard.TextEntry(item.UserName);
                            break;
                        case "totp":
                            iis.Keyboard.TextEntry(item.Totp);
                            break;
                        case "usernamepassword":
                            iis.Keyboard.TextEntry(item.UserName);
                            iis.Keyboard.KeyDown(VirtualKeyCode.TAB);
                            iis.Keyboard.TextEntry(item.Password);
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Connection.ShowAlert();
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);
            }

            Connection.ShowOk();
        }

        private async Task<Item> GetItem()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Getting {settings.SelectedItemInformation} of item {settings.ItemName}");

            var cmd = BwCliWrapper.GetCli().WithArguments(new[] {"get", "item", settings.ItemName});
            var result = await cmd.ExecuteBufferedAsync();
            
            var x = JObject.Parse(result.StandardOutput);
            
            return JsonConvert.DeserializeObject<Item>(x.SelectToken("login")?.ToString() ?? string.Empty);
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
            LoadItems().GetAwaiter().GetResult();

            SaveSettings();
        }

        private async Task LoadItems()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Load Items");

            var cmd = BwCliWrapper.GetCli().WithArguments(new[] {"list", "items"});
            var result = await cmd.ExecuteBufferedAsync();
            settings.Items = JsonConvert.DeserializeObject<List<ItemListDto>>(result.StandardOutput);
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{settings.Items.Count} Items Loaded");
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