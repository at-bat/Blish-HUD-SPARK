using System.Collections.Generic;

namespace rp.spark.Models
{
    public class ProfileDiscoveryTags
    {
        public List<string> Preferences { get; set; } = new List<string>();

        public List<string> Themes { get; set; } = new List<string>();

        public List<string> Styles { get; set; } = new List<string>();
    }
}