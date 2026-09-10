using System;
using System.Collections.Generic;
using System.Linq;
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

            /// <summary>
            /// Stamped with a fresh value by the Load button. Its only purpose is to tell a
            /// deliberate reload apart from an ordinary settings change such as typing.
            /// </summary>
            [JsonProperty(PropertyName = "loadtoken")]
            public string LoadToken { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;
        private readonly IBwCli cli;
        private readonly IKeyboardTyper typer;
        private string handledLoadToken;

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
                switch (settings.SelectedItemInformation)
                {
                    case "totp":
                        // Asked for separately on purpose: 'bw get item' only carries the
                        // TOTP secret, and typing that into a login form is useless.
                        await typer.TypeText(await GetTotpCode());
                        break;
                    case "password":
                        await typer.TypeText((await GetItem()).Password);
                        break;
                    case "username":
                        await typer.TypeText((await GetItem()).UserName);
                        break;
                    case "usernamepassword":
                        Item item = await GetItem();
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

            string output = await cli.Run("get", "item", ResolveItemQuery());

            return ParseItem(output);
        }

        /// <summary>
        /// What to hand the CLI for the configured selection.
        ///
        /// The picker stores whatever is in its search box. When that matches one of the
        /// loaded entries, its id is used, which is exact and immune to duplicate names -
        /// and necessary, because the label carries the username in parentheses and the CLI
        /// would never find that. Anything else is passed through as a search term, which
        /// also covers a settings file written before the picker changed, where the stored
        /// value is already an id.
        /// </summary>
        internal string ResolveItemQuery()
        {
            string selection = settings.ItemName;

            if (string.IsNullOrWhiteSpace(selection))
            {
                throw new InvalidOperationException("No vault item selected");
            }

            selection = selection.Trim();

            ItemListDto match = settings.Items?.FirstOrDefault(
                item => string.Equals(item.ItemName, selection, StringComparison.Ordinal));

            if (match != null && match.ItemId != Guid.Empty)
            {
                return match.ItemId.ToString();
            }

            return selection;
        }

        /// <summary>
        /// Turns 'bw list items' output into the picker's entries, labelling each one with
        /// its username so several logins for the same site can be told apart.
        /// </summary>
        internal static List<ItemListDto> ParseItemList(string commandOutput)
        {
            List<ItemListDto> items =
                JsonConvert.DeserializeObject<List<ItemListDto>>(commandOutput) ?? new List<ItemListDto>();

            foreach (ItemListDto item in items)
            {
                item.ApplyDisplayName();
            }

            return items;
        }

        /// <summary>
        /// The current one time code for the item.
        ///
        /// This deliberately does not come from 'bw get item': that returns the item's
        /// stored TOTP *secret* (a base32 seed such as JBSWY3DPEHPK3PXP), not the six digit
        /// code a login form expects. The seed looks enough like a password to be mistaken
        /// for one. 'bw get totp' computes the current code instead.
        /// </summary>
        internal async Task<string> GetTotpCode()
        {
            string code = (await cli.Run("get", "totp", ResolveItemQuery())).Trim();

            if (string.IsNullOrEmpty(code))
            {
                throw new InvalidOperationException($"No TOTP is configured for '{settings.ItemName}'");
            }

            return code;
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

            // Every keystroke in the search box arrives here. Reloading the vault each time
            // ran a Bitwarden CLI process per character, and saving afterwards echoed the
            // settings back to the property inspector, which rewrote the box mid-typing and
            // made characters jump and disappear. The list is fetched once, when the Load
            // button asks for it, and filtered in the inspector from then on.
            if (!ShouldReloadItems())
            {
                return;
            }

            LoadItems().GetAwaiter().GetResult();

            // The item list is new information the inspector does not have, so this is the
            // one case worth sending back.
            SaveSettings();
        }

        /// <summary>
        /// True only when the Load button has been pressed since the last reload. The button
        /// stamps a fresh token into the settings; typing leaves it untouched.
        /// </summary>
        internal bool ShouldReloadItems()
        {
            if (string.IsNullOrEmpty(settings.LoadToken) || settings.LoadToken == handledLoadToken)
            {
                return false;
            }

            handledLoadToken = settings.LoadToken;
            return true;
        }

        internal async Task LoadItems()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Load Items");

            try
            {
                string output = await cli.Run("list", "items");
                settings.Items = ParseItemList(output);
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
