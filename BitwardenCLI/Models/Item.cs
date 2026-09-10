using System;
using Newtonsoft.Json;

namespace BitwardenStreamdeckPlugin.Models
{
    internal class Item
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "username")]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "password")]
        public string Password { get; set; }

        /// <summary>
        /// The stored TOTP seed, not a usable one time code. Use 'bw get totp' for the
        /// current code; typing this into a login form would never work.
        /// </summary>
        [JsonProperty(PropertyName = "totp")]
        public string TotpSecret { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid Id { get; set; }
    }
}
