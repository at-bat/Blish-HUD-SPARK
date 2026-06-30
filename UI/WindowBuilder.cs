using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace rp.spark.UI
{
    internal class WindowBuilder
    {
        private static readonly Logger Logger = Logger.GetLogger<WindowBuilder>();

        private const int WindowBackgroundAssetId = 155985;
        private const int WindowEmblemAssetId = 3307061;
        private const int TabbedWindowContentX = 96;
        private const int TabbedWindowContentY = 22;
        private const int TabbedWindowContentWidth = 783;
        private const int TabbedWindowContentBottom = 676;

        private static readonly Rectangle StandardWindowBounds = new Rectangle(40, 26, 913, 691);
        private static readonly Rectangle TabbedWindowContentBounds = new Rectangle(
            TabbedWindowContentX,
            TabbedWindowContentY,
            TabbedWindowContentWidth,
            TabbedWindowContentBottom - TabbedWindowContentY);

        private readonly Dictionary<int, AsyncTexture2D> _assetIcons = new Dictionary<int, AsyncTexture2D>();
        private readonly Dictionary<WindowBase2, EmblemBinding> _emblemBindings = new Dictionary<WindowBase2, EmblemBinding>();

        private AsyncTexture2D _windowBackground;
        private AsyncTexture2D _windowEmblem;

        public TabbedWindow2 MakeTabbedWindow(string subtitle, string id)
        {
            var window = new TabbedWindow2(
                GetWindowBackground(),
                StandardWindowBounds,
                TabbedWindowContentBounds)
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "SPARK",
                Subtitle = subtitle,
                SavesPosition = true,
                Id = id
            };

            AttachEmblem(window);
            return window;
        }

        public StandardWindow MakeWindow(string subtitle, string id, Rectangle contentBounds)
        {
            var window = new StandardWindow(
                GetWindowBackground(),
                StandardWindowBounds,
                contentBounds)
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "SPARK",
                Subtitle = subtitle,
                SavesPosition = true,
                Id = id
            };

            AttachEmblem(window);
            return window;
        }

        public AsyncTexture2D IconFromAsset(int assetId)
        {
            if (assetId <= 0)
                return null;

            if (_assetIcons.TryGetValue(assetId, out var icon))
                return icon;

            icon = GameService.Content.DatAssetCache.GetTextureFromAssetId(assetId);
            _assetIcons[assetId] = icon;

            return icon;
        }

        public void DisposeWindow(WindowBase2 window)
        {
            if (window == null)
                return;

            try
            {
                ReleaseEmblem(window);
                window.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "SPARK failed to dispose a window.");
            }
        }

        public void Clear()
        {
            foreach (var binding in _emblemBindings.Values)
                binding.Dispose();

            _emblemBindings.Clear();
            _assetIcons.Clear();
            _windowBackground = null;
            _windowEmblem = null;
        }

        private AsyncTexture2D GetWindowBackground()
        {
            return _windowBackground
                   ?? (_windowBackground = GameService.Content.DatAssetCache.GetTextureFromAssetId(WindowBackgroundAssetId));
        }

        private AsyncTexture2D GetWindowEmblem()
        {
            return _windowEmblem
                   ?? (_windowEmblem = GameService.Content.DatAssetCache.GetTextureFromAssetId(WindowEmblemAssetId));
        }

        private void AttachEmblem(WindowBase2 window)
        {
            var emblem = GetWindowEmblem();

            if (window == null || emblem == null)
                return;

            ReleaseEmblem(window);
            _emblemBindings[window] = EmblemBinding.Attach(window, emblem);
        }

        private void ReleaseEmblem(WindowBase2 window)
        {
            if (window == null || !_emblemBindings.TryGetValue(window, out var binding))
                return;

            binding.Dispose();
            _emblemBindings.Remove(window);
        }

        private sealed class EmblemBinding : IDisposable
        {
            private readonly WindowBase2 _window;
            private readonly AsyncTexture2D _emblem;

            private EmblemBinding(WindowBase2 window, AsyncTexture2D emblem)
            {
                _window = window;
                _emblem = emblem;
            }

            public static EmblemBinding Attach(WindowBase2 window, AsyncTexture2D emblem)
            {
                var binding = new EmblemBinding(window, emblem);

                window.Emblem = emblem.Texture;

                if (!emblem.HasSwapped)
                    emblem.TextureSwapped += binding.HandleTextureSwapped;

                return binding;
            }

            public void Dispose()
            {
                _emblem.TextureSwapped -= HandleTextureSwapped;
            }

            private void HandleTextureSwapped(object sender, ValueChangedEventArgs<Texture2D> e)
            {
                _window.Emblem = e.NewValue;
            }
        }
    }
}
