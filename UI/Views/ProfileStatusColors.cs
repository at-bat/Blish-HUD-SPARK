using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace rp.spark.UI.Views
{
    internal static class ProfileStatusColors
    {
        public static Color Get(RPStatus status)
        {
            switch (status)
            {
                case RPStatus.Looking: return new Color(150, 255, 80);
                case RPStatus.Busy: return new Color(255, 150, 40);
                case RPStatus.Offline: return new Color(160, 160, 160);
                case RPStatus.Invisible: return new Color(226, 226, 226);
                default: return new Color(120, 210, 255);
            }
        }
    }
}
