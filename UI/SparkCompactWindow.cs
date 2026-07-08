using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace rp.spark.UI
{
    internal sealed class SparkCompactWindow : Panel
    {
        private const int HeaderHeight = 46;
        private const int TitleBarVerticalOffset = 11;
        private const int LeftTitleBarHorizontalOffset = 2;
        private const int RightTitleBarHorizontalOffset = 16;
        private const int ContentPadding = 10;
        private const int TitleTextY = 1;
        private const int CloseButtonSize = 26;
        private const int CloseButtonTop = 3;
        private const int CloseButtonRightPadding = 6;

        private static readonly Texture2D TextureTitleBarLeft = Content.GetTexture("titlebar-inactive");
        private static readonly Texture2D TextureTitleBarRight = Content.GetTexture("window-topright");
        private static readonly Texture2D TextureTitleBarLeftActive = Content.GetTexture("titlebar-active");
        private static readonly Texture2D TextureTitleBarRightActive = Content.GetTexture("window-topright-active");

        private readonly AsyncTexture2D _background;
        private readonly Rectangle _backgroundSource;
        private readonly ViewContainer _viewContainer;
        private readonly Label _titleLabel;
        private readonly StandardButton _closeButton;

        private Texture2D _windowBackgroundTexture;
        private bool _dragging;
        private Point _dragStart;
        private Rectangle _leftTitleBarDrawBounds = Rectangle.Empty;
        private Rectangle _rightTitleBarDrawBounds = Rectangle.Empty;
        private bool _mouseOverTitleBar;

        public event Action<Point> LocationSaved;
        private bool _movedDuringDrag;

        public SparkCompactWindow(string title, AsyncTexture2D background, Rectangle backgroundSource, Point size)
        {
            _background = background;
            _backgroundSource = backgroundSource;

            Size = size;
            Parent = GameService.Graphics.SpriteScreen;
            Visible = false;
            ClipsBounds = true;
            ShowBorder = false;
            BackgroundColor = Color.Transparent;
            ZIndex = 1000;

            if (_background != null)
            {
                _windowBackgroundTexture = _background.Texture;

                if (!_background.HasSwapped)
                    _background.TextureSwapped += HandleTextureSwapped;
            }

            _titleLabel = new Label
            {
                Text = title,
                Font = GameService.Content.DefaultFont18,
                TextColor = Color.White,
                StrokeText = true,
                Location = new Point(ContentPadding, TitleTextY),
                Parent = this
            };

            _closeButton = new StandardButton
            {
                Text = "X",
                Size = new Point(CloseButtonSize, CloseButtonSize),
                Parent = this
            };

            _closeButton.Click += delegate
            {
                Visible = false;
            };

            _viewContainer = new ViewContainer
            {
                Parent = this,
                FadeView = false
            };

            ApplyLayout();

            GameService.Input.Mouse.LeftMouseButtonReleased += HandleGlobalMouseReleased;
        }

        public void Show(IView view)
        {
            _viewContainer.Show(view);
            Visible = true;
            ZIndex = 1000;
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            if (_dragging)
            {
                var delta = GameService.Input.Mouse.Position - _dragStart;

                if (delta.X != 0 || delta.Y != 0)
                {
                    Location += delta;
                    _movedDuringDrag = true;
                }

                _dragStart = GameService.Input.Mouse.Position;
            }

            base.UpdateContainer(gameTime);
        }

        protected override CaptureType CapturesInput()
        {
            return CaptureType.Mouse | CaptureType.MouseWheel;
        }

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e)
        {
            ZIndex = 1000;

            _mouseOverTitleBar = IsInTitleDragRegion(RelativeMousePosition);

            if (_mouseOverTitleBar)
            {
                _dragging = true;
                _movedDuringDrag = false;
                _dragStart = GameService.Input.Mouse.Position;
            }

            base.OnLeftMouseButtonPressed(e);
        }

        protected override void OnMouseMoved(MouseEventArgs e)
        {
            _mouseOverTitleBar = IsInTitleDragRegion(RelativeMousePosition);
            base.OnMouseMoved(e);
        }

        protected override void OnMouseLeft(MouseEventArgs e)
        {
            _mouseOverTitleBar = false;
            base.OnMouseLeft(e);
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (_windowBackgroundTexture != null)
            {
                var destination = new Rectangle(
                    AbsoluteBounds.X,
                    AbsoluteBounds.Y,
                    Math.Max(0, Width),
                    Math.Max(0, Height));

                spriteBatch.Draw(_windowBackgroundTexture, destination, _backgroundSource, Color.White);
            }

            DrawTitleBar(spriteBatch);
        }

        private void DrawTitleBar(SpriteBatch spriteBatch)
        {
            var active = _dragging || _mouseOverTitleBar;

            spriteBatch.DrawOnCtrl(
                this,
                active ? TextureTitleBarLeftActive : TextureTitleBarLeft,
                _leftTitleBarDrawBounds);

            spriteBatch.DrawOnCtrl(
                this,
                active ? TextureTitleBarRightActive : TextureTitleBarRight,
                _rightTitleBarDrawBounds);
        }

        private bool IsInTitleDragRegion(Point position)
        {
            return position.Y >= 0
                && position.Y < HeaderHeight
                && position.X >= 0
                && position.X < Width - CloseButtonSize - 12;
        }

        private void ApplyTitleBarLayout()
        {
            var titleBarBounds = new Rectangle(0, 0, Width, HeaderHeight);

            _rightTitleBarDrawBounds = new Rectangle(
                titleBarBounds.Width - TextureTitleBarRight.Width + RightTitleBarHorizontalOffset,
                titleBarBounds.Y - TitleBarVerticalOffset,
                TextureTitleBarRight.Width,
                TextureTitleBarRight.Height);

            _leftTitleBarDrawBounds = new Rectangle(
                titleBarBounds.X - LeftTitleBarHorizontalOffset,
                titleBarBounds.Y - TitleBarVerticalOffset,
                Math.Max(0, Math.Min(TextureTitleBarLeft.Width, _rightTitleBarDrawBounds.Left - LeftTitleBarHorizontalOffset)),
                TextureTitleBarLeft.Height);
        }

        private void ApplyLayout()
        {
            ApplyTitleBarLayout();
            _titleLabel.Size = new Point(Math.Max(0, Width - 62), HeaderHeight - TitleTextY);
            _closeButton.Size = new Point(CloseButtonSize, CloseButtonSize);
            _closeButton.Location = new Point(
                Width - CloseButtonSize - CloseButtonRightPadding,
                CloseButtonTop);

            _viewContainer.Location = new Point(ContentPadding, HeaderHeight);
            _viewContainer.Size = new Point(
                Math.Max(0, Width - ContentPadding * 2),
                Math.Max(0, Height - HeaderHeight - ContentPadding));
        }

        private void HandleGlobalMouseReleased(object sender, MouseEventArgs e)
        {
            if (_dragging && _movedDuringDrag)
                LocationSaved?.Invoke(Location);

            _dragging = false;
            _movedDuringDrag = false;
        }

        private void HandleTextureSwapped(object sender, ValueChangedEventArgs<Texture2D> e)
        {
            _windowBackgroundTexture = e.NewValue;
        }

        protected override void DisposeControl()
        {
            if (_background != null)
                _background.TextureSwapped -= HandleTextureSwapped;

            GameService.Input.Mouse.LeftMouseButtonReleased -= HandleGlobalMouseReleased;

            _viewContainer?.Clear();

            base.DisposeControl();
        }
    }
}