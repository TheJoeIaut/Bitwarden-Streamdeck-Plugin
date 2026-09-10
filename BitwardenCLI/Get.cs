using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BitwardenStreamdeckPlugin.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitwardenStreamdeckPlugin
{
    [PluginActionId("com.thejoeiaut.bitwardenget")]
    public class Get : KeypadBase
    {
        internal class PluginSettings
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
        private readonly IBwCli cli;
        private readonly IKeyboardTyper typer;

        #endregion

        public Get(ISDConnection connection, InitialPayload payload)
            : this(connection, payload, BwCli.Shared, KeyboardTyper.Shared)
        {
        }

        internal Get(ISDConnection connection, InitialPayload payload, IBwCli cli, IKeyboardTyper typer)
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
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            // Typing can take a while for long secrets, so it must not block the Stream Deck
            // event loop. Failures are reported from inside TypeSelectedInformation.
            Task.Run(TypeSelectedInformation);
        }

        internal async Task TypeSelectedInformation()
        {
            try
            {
                Item item = await GetItem();

                switch (settings.SelectedItemInformation)
                {
                    case "password":
                        await typer.TypeText(item.Password);
                        break;
                    case "username":
                        await typer.TypeText(item.UserName);
                        break;
                    case "totp":
                        await typer.TypeText(item.Totp);
                        break;
                    case "usernamepassword":
                        await typer.TypeText(item.UserName);
                        await typer.PressTab();
                        await typer.TypeText(item.Password);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"No item information selected (got '{settings.SelectedItemInformation}')");
                }

                await Connection.ShowOk();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);
                await Connection.ShowAlert();
            }
        }

        internal async Task<Item> GetItem()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO,
                $"Getting {settings.SelectedItemInformation} of item {settings.ItemName}");

            string output = await cli.Run("get", "item", settings.ItemName);

            return ParseItem(output);
        }

        /// <summary>
        /// 'bw get item' returns the whole vault entry; the credentials live under "login".
        /// </summary>
        internal static Item ParseItem(string commandOutput)
        {
            JObject parsed = JObject.Parse(commandOutput);
            JToken login = parsed.SelectToken("login");

            if (login == null)
            {
                throw new InvalidOperationException("Bitwarden item has no 'login' section");
            }

            return JsonConvert.DeserializeObject<Item>(login.ToString());
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

        internal async Task LoadItems()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Load Items");

            try
            {
                string output = await cli.Run("list", "items");
                settings.Items = JsonConvert.DeserializeObject<List<ItemListDto>>(output);
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{settings.Items.Count} Items Loaded");
            }
            catch (Exception ex)
            {
                // A locked vault is the usual cause here. Leave the previous list in place
                // rather than letting the exception escape into the Stream Deck event loop.
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Could not load items: {ex.Message}");
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
