using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace rp.spark.UI.Controls
{
    internal class AssetIcon : Panel
    {
        private AsyncTexture2D _texture;
        private EventHandler<ValueChangedEventArgs<Texture2D>> _textureSwappedHandler;

        public int AssetId { get; private set; }

        public void SetAssetId(int assetId)
        {
            ClearTexture();
            AssetId = assetId;

            if (assetId <= 0)
                return;

            _texture = GameService.Content.DatAssetCache.GetTextureFromAssetId(assetId);

            if (_texture == null)
                return;

            BackgroundTexture = _texture.Texture;
            _textureSwappedHandler = (s, e) => BackgroundTexture = e.NewValue;
            _texture.TextureSwapped += _textureSwappedHandler;
        }

        private void ClearTexture()
        {
            if (_texture != null && _textureSwappedHandler != null)
                _texture.TextureSwapped -= _textureSwappedHandler;

            _texture = null;
            _textureSwappedHandler = null;
            BackgroundTexture = null;
        }

        protected override void DisposeControl()
        {
            ClearTexture();
            base.DisposeControl();
        }
    }
}
