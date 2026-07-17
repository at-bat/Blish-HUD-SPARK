using System.Linq;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Controls.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace rp.spark.UI.Controls
{
    internal sealed class ContextMenuColours : ContextMenuStripItem
    {
        private const int BulletSize = 18;
        private const int HorizontalPadding = 6;
        private const int TextLeftPadding = HorizontalPadding + BulletSize + HorizontalPadding;

        private readonly AsyncTexture2D _bulletTexture = AsyncTexture2D.FromAssetId(155038);
        private static readonly Texture2D SubmenuArrowTexture = Content.GetTexture("context-menu-strip-submenu");

        public Color TextColor { get; set; } = StandardColors.Default;

        public ContextMenuColours(string text, Color textColor)
        {
            Text = text;
            TextColor = textColor;
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var iconTint = Enabled
                ? MouseOver ? StandardColors.Tinted : StandardColors.Default
                : StandardColors.DisabledText;

            if (CanCheck)
            {
                var checkState = Checked ? "-checked" : "-unchecked";
                var checkStyle = !Enabled ? "-disabled" : MouseOver ? "-active" : string.Empty;

                var checkbox = Checkable.TextureRegionsCheckbox
                    .First(region => region.Name == $"checkbox/cb{checkState}{checkStyle}");

                spriteBatch.DrawOnCtrl(this, checkbox,
                    new Rectangle(HorizontalPadding + BulletSize / 2 - 16, Height / 2 - 16, 32, 32),
                    StandardColors.Default);
            }
            else
            {
                spriteBatch.DrawOnCtrl(this, _bulletTexture,
                    new Rectangle(HorizontalPadding, Height / 2 - BulletSize / 2, BulletSize, BulletSize),
                    iconTint);
            }

            var textBounds = new Rectangle(
                TextLeftPadding,
                0,
                Width - TextLeftPadding - HorizontalPadding,
                Height);

            spriteBatch.DrawStringOnCtrl(this, Text, Content.DefaultFont14,
                new Rectangle(textBounds.X + 1, textBounds.Y + 1, textBounds.Width, textBounds.Height),
                StandardColors.Shadow);

            spriteBatch.DrawStringOnCtrl(this, Text, Content.DefaultFont14, textBounds,
                Enabled ? TextColor : StandardColors.DisabledText);

            if (Submenu != null)
            {
                spriteBatch.DrawOnCtrl(this, SubmenuArrowTexture,
                    new Rectangle(
                        Width - HorizontalPadding - SubmenuArrowTexture.Width,
                        Height / 2 - SubmenuArrowTexture.Height / 2,
                        SubmenuArrowTexture.Width,
                        SubmenuArrowTexture.Height),
                    iconTint);
            }
        }
    }
}