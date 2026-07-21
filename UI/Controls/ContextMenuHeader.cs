using System;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace rp.spark.UI.Controls
{
    internal sealed class ContextMenuHeader : ContextMenuStripItem
    {
        private static readonly Color HeaderTextColor = new Color(255, 233, 180);
        private static readonly Color LineColor = new Color(255, 233, 180) * 0.35f;

        public ContextMenuHeader(string text)
        {
            Text = text;
            Enabled = false;
            EffectBehind = null;
        }

        public override void DoUpdate(GameTime gameTime)
        {
            base.DoUpdate(gameTime);
            Height = 20;
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var textSize = Content.DefaultFont14.MeasureString(Text);
            var textWidth = (int)Math.Ceiling(textSize.Width);
            var textX = Math.Max(26, (Width - textWidth) / 2);
            var lineY = Height / 2 + 1;

            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel,
                new Rectangle(8, lineY, Math.Max(0, textX - 16), 1),
                LineColor);

            var rightLineX = textX + textWidth + 8;

            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel,
                new Rectangle(rightLineX, lineY, Math.Max(0, Width - rightLineX - 8), 1),
                LineColor);

            var textBounds = new Rectangle(textX, 0, textWidth + 4, Height);

            spriteBatch.DrawStringOnCtrl(this, Text, Content.DefaultFont14,
                new Rectangle(textBounds.X + 1, textBounds.Y + 1, textBounds.Width, textBounds.Height),
                StandardColors.Shadow);

            spriteBatch.DrawStringOnCtrl(this, Text, Content.DefaultFont14,
                textBounds,
                HeaderTextColor * 0.9f);
        }
    }
}