using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace rp.spark.Models
{
    public class AtAGlanceEntry
    {
        public int AssetId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Tooltip { get; set; } = string.Empty;
    }
}