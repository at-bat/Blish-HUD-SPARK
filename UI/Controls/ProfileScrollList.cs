using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace rp.spark.UI.Controls
{
    internal class ProfileScrollList : Panel
    {
        private const int ScrollbarWidth = 12;
        private const int ScrollbarGap = 4;

        private readonly int _listWidth;
        private readonly int _rowHeight;
        private readonly int _rowGap;
        private readonly Panel _rowsPanel;
        private readonly Scrollbar _scrollbar;

        public ProfileScrollList(int listWidth, int listHeight, int rowHeight, int rowGap = 4)
        {
            _listWidth = listWidth;
            _rowHeight = rowHeight;
            _rowGap = rowGap;

            ShowBorder = false;
            Size = new Point(listWidth + ScrollbarGap + ScrollbarWidth, listHeight);

            _rowsPanel = new MouseWheelPanel
            {
                ShowBorder = false,
                Location = Point.Zero,
                Size = new Point(listWidth, listHeight),
                BackgroundColor = new Color(0, 0, 0, 0),
                ClipsBounds = true,
                Parent = this
            };

            _scrollbar = new Scrollbar(_rowsPanel)
            {
                Location = new Point(listWidth + ScrollbarGap, 0),
                Size = new Point(ScrollbarWidth, listHeight),
                Parent = this
            };
        }

        public Panel AddRow(int index, string tooltipText)
        {
            return new Panel
            {
                ShowBorder = false,
                Location = new Point(0, index * (_rowHeight + _rowGap)),
                Size = new Point(_listWidth, _rowHeight),
                BackgroundColor = index % 2 == 0
                    ? new Color(0, 0, 0, 70)
                    : new Color(20, 20, 20, 70),
                Parent = _rowsPanel,
                BasicTooltipText = tooltipText ?? string.Empty
            };
        }

        public Label AddCell(Container parent, string text, int x, int y, int width, Color color)
        {
            return new Label
            {
                Text = string.IsNullOrWhiteSpace(text) ? "-" : text,
                Font = GameService.Content.DefaultFont14,
                TextColor = color,
                WrapText = false,
                Location = new Point(x, y),
                Size = new Point(width, Math.Max(24, _rowHeight - y)),
                Parent = parent
            };
        }

        public void ShowEmptyMessage(string text)
        {
            ClearRows();

            new Label
            {
                Text = text,
                Font = GameService.Content.DefaultFont16,
                TextColor = new Color(220, 220, 220),
                Location = new Point(12, 12),
                Size = new Point(Math.Max(0, _listWidth - 24), 30),
                Parent = _rowsPanel
            };
        }

        public void ClearRows(bool resetScroll = true)
        {
            foreach (var child in _rowsPanel.Children.ToArray())
                child.Dispose();

            if (resetScroll)
                ResetScroll();
        }

        public void ResetScroll()
        {
            _rowsPanel.VerticalScrollOffset = 0;

            if (_scrollbar != null)
                _scrollbar.ScrollDistance = 0;
        }

        public static void WireInteraction(Control control, string tooltipText, Action click)
        {
            control.BasicTooltipText = tooltipText ?? string.Empty;
            control.Click += (s, e) => click?.Invoke();
        }

        private class MouseWheelPanel : Panel
        {
            protected override CaptureType CapturesInput()
            {
                return CaptureType.Mouse | CaptureType.MouseWheel;
            }
        }
    }
}
