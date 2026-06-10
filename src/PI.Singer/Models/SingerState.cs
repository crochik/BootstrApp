using System.Collections.Generic;
using Newtonsoft.Json;

namespace Models
{
    public class SingerState
    {
        [JsonProperty("current_stream")]
        public string CurrentStream { get; set; }

        [JsonProperty("bookmarks")]
        public Dictionary<string, SingerBookmark> Bookmarks { get; set; }
    }
}