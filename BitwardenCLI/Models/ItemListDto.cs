using System;
using Newtonsoft.Json;

namespace BitwardenStreamdeckPlugin.Models
{
    /// <summary>
    /// One entry in the property inspector's item picker.
    ///
    /// 'bw list items' returns whole vault entries, passwords and TOTP seeds included.
    /// Only the fields declared here are ever deserialized, and the list is written into
    /// the action's Stream Deck settings, so nothing secret may be added to this type.
    /// </summary>
    internal class ItemListDto
    {
        /// <summary>
        /// The label shown in the picker. After <see cref="ApplyDisplayName"/> this is the
        /// entry name followed by the username in parentheses.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string ItemName { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Read from the CLI output purely to get at the username. Never stored: see
        /// <see cref="ShouldSerializeLogin"/>.
        /// </summary>
        [JsonProperty(PropertyName = "login")]
        public ItemListLoginDto Login { get; set; }

        /// <summary>
        /// Newtonsoft calls this when serializing. Keeping the login out of the saved
        /// settings means the picker's data can never carry credentials.
        /// </summary>
        public bool ShouldSerializeLogin() => false;

        /// <summary>
        /// Appends the username so several logins for the same site can be told apart,
        /// turning "github.com" into "github.com (octocat)". Entries without a username -
        /// secure notes, cards - keep their plain name.
        /// </summary>
        internal void ApplyDisplayName()
        {
            string userName = Login?.UserName;

            if (!string.IsNullOrWhiteSpace(userName))
            {
                ItemName = $"{ItemName} ({userName})";
            }
        }
    }

    /// <summary>
    /// The only part of an entry's login section the picker needs. Deliberately does not
    /// declare password or totp.
    /// </summary>
    internal class ItemListLoginDto
    {
        [JsonProperty(PropertyName = "username")]
        public string UserName { get; set; }
    }
}
