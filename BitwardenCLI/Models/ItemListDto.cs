using System;
using Newtonsoft.Json;

namespace BitwardenStreamdeckPlugin.Models
{
    internal class ItemListDto
    {
        [JsonProperty(PropertyName = "name")]
        public string ItemName { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid ItemId { get; set; }
    }
}
