using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace rp.spark.UI.Controls
{
    internal sealed class PageList
    {
        public const int DefaultPageSize = 50;

        public PageList(int pageSize = DefaultPageSize)
        {
            PageSize = Math.Max(1, pageSize);
        }

        public int PageIndex { get; private set; }

        public int PageSize { get; }

        public int GetPageCount(int itemCount)
        {
            itemCount = Math.Max(0, itemCount);

            return Math.Max(
                1,
                (itemCount + PageSize - 1) / PageSize);
        }

        public void Reset()
        {
            PageIndex = 0;
        }

        public void Clamp(int itemCount)
        {
            PageIndex = Math.Max(
                0,
                Math.Min(PageIndex, GetPageCount(itemCount) - 1));
        }

        public bool Previous()
        {
            if (PageIndex <= 0)
                return false;

            PageIndex--;
            return true;
        }

        public bool Next(int itemCount)
        {
            if (PageIndex + 1 >= GetPageCount(itemCount))
                return false;

            PageIndex++;
            return true;
        }

        public int GetFirstItemNumber(int itemCount)
        {
            return itemCount <= 0
                ? 0
                : PageIndex * PageSize + 1;
        }

        public int GetLastItemNumber(int itemCount)
        {
            return itemCount <= 0
                ? 0
                : Math.Min((PageIndex + 1) * PageSize, itemCount);
        }

        public IReadOnlyList<T> GetPage<T>(IReadOnlyList<T> items)
        {
            var itemCount = items?.Count ?? 0;

            Clamp(itemCount);

            if (itemCount == 0)
                return new List<T>();

            var start = PageIndex * PageSize;
            var count = Math.Min(PageSize, itemCount - start);
            var page = new List<T>(count);

            for (var index = start; index < start + count; index++)
                page.Add(items[index]);

            return page;
        }
    }

    internal sealed class PageListControls
    {
        private const int ButtonWidth = 90;
        private const int ControlHeight = 30;
        private const int Gap = 10;

        private readonly PageList _pageList;
        private readonly Action _pageChanged;
        private readonly StandardButton _previousButton;
        private readonly StandardButton _nextButton;
        private readonly Label _pageLabel;

        private int _itemCount;

        public PageListControls(
            Container parent,
            PageList pageList,
            int width,
            Action pageChanged)
        {
            _pageList = pageList ?? throw new ArgumentNullException(nameof(pageList));
            _pageChanged = pageChanged;

            Root = new Panel
            {
                ShowBorder = false,
                Size = new Point(width, ControlHeight),
                Parent = parent
            };

            _previousButton = new StandardButton
            {
                Text = "Previous",
                Location = Point.Zero,
                Size = new Point(ButtonWidth, ControlHeight),
                Parent = Root
            };

            _nextButton = new StandardButton
            {
                Text = "Next",
                Location = new Point(width - ButtonWidth, 0),
                Size = new Point(ButtonWidth, ControlHeight),
                Parent = Root
            };

            _pageLabel = new Label
            {
                Text = string.Empty,
                Font = GameService.Content.DefaultFont12,
                TextColor = new Color(220, 220, 220),
                Location = new Point(ButtonWidth + Gap, 4),
                Size = new Point(
                    Math.Max(0, width - (ButtonWidth + Gap) * 2),
                    24),
                Parent = Root
            };

            _previousButton.Click += (s, e) =>
            {
                if (!_pageList.Previous())
                    return;

                Update(_itemCount);
                _pageChanged?.Invoke();
            };

            _nextButton.Click += (s, e) =>
            {
                if (!_pageList.Next(_itemCount))
                    return;

                Update(_itemCount);
                _pageChanged?.Invoke();
            };

            _previousButton.Enabled = false;
            _nextButton.Enabled = false;
            _pageLabel.Text = "Page 1 of 1.";
        }

        public Panel Root { get; }

        public Point Location
        {
            get => Root.Location;
            set => Root.Location = value;
        }

        public void Update(int itemCount)
        {
            _itemCount = Math.Max(0, itemCount);
            _pageList.Clamp(_itemCount);

            var pageCount = _pageList.GetPageCount(_itemCount);

            _previousButton.Enabled = _pageList.PageIndex > 0;
            _nextButton.Enabled = _pageList.PageIndex + 1 < pageCount;

            _pageLabel.Text =
                $"Page {_pageList.PageIndex + 1} of {pageCount}.";
        }
    }
}