using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.thejoeiaut.bitwarden.Models
{
    internal class ItemListDto
    {
        [JsonProperty(PropertyName = "name")]
        public string ItemName { get; set; }

        [JsonProperty(PropertyName = "id")]
        public Guid ItemId { get; set; }
    }
}
