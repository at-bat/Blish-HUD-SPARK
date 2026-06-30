using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using rp.spark.Services;
using rp.spark.UI.Controls;
using System;
using System.Linq;

namespace rp.spark.UI.Views
{
    internal sealed class SparkBlocklist : IDisposable
    {
        private const int InputHeight = 30;
        private const int DefaultListHeight = 360;
        private const int RowHeight = 30;
        private const int ActionButtonWidth = 90;

        private readonly SparkSettings _settings;
        private readonly Func<string, string> _blockAccount;
        private readonly Func<string, string> _unblockAccount;
        private readonly Action<Action> _watchBlocks;
        private readonly Action<Action> _unwatchBlocks;
        private readonly PageList _page = new PageList();
        private PageListControls _pageControls;

        private bool _isDisposed;
        private TextBox _input;
        private ProfileScrollList _list;
        private Label _status;

        public SparkBlocklist(
            SparkSettings settings,
            Func<string, string> blockAccount,
            Func<string, string> unblockAccount,
            Action<Action> watchBlocklistChanged,
            Action<Action> unwatchBlocklistChanged)
        {
            _settings = settings;
            _blockAccount = blockAccount;
            _unblockAccount = unblockAccount;
            _watchBlocks = watchBlocklistChanged;
            _unwatchBlocks = unwatchBlocklistChanged;
        }

        public void Build(FlowPanel settingsStack, int contentWidth, int listHeight = DefaultListHeight)
        {
            _isDisposed = false;

            var blockedStack = SparkFormLayout.AddAutoStack(settingsStack, contentWidth, 5);
            var inputRow = SparkFormLayout.AddRow(blockedStack, contentWidth, InputHeight, 8);

            _input = SparkFormLayout.AddTextBox(
                inputRow,
                string.Empty,
                "Account name, such as Name.1234",
                contentWidth - ActionButtonWidth - 8,
                InputHeight,
                ProfileLimits.MaxAccountNameLength);

            var addButton = SparkFormLayout.AddButton(inputRow, "Block", ActionButtonWidth, InputHeight);
            addButton.Click += (s, e) => AddBlock();
            _input.EnterPressed += (s, e) => AddBlock();

            _list = new ProfileScrollList(contentWidth - 16, listHeight, RowHeight)
            {
                Parent = blockedStack
            };

            _pageControls = new PageListControls(
                blockedStack,
                _page,
                contentWidth,
                () => Refresh(false));

            _status = SparkFormLayout.AddLabel(
                blockedStack,
                string.Empty,
                contentWidth,
                24,
                GameService.Content.DefaultFont12,
                SparkViewUI.SecondaryTextColor);

            _watchBlocks?.Invoke(OnBlocksChanged);
            Refresh(true);
        }

        private void OnBlocksChanged()
        {
            SparkUiThread.Queue(() =>
            {
                if (!_isDisposed)
                    Refresh(false);
            });
        }

        private void AddBlock()
        {
            var accountName = _input?.Text?.Trim() ?? string.Empty;

            if (!SparkSettings.IsValidAccountName(accountName))
            {
                SetStatus("Enter an account name like Name.1234.");
                return;
            }

            SetStatus(_blockAccount?.Invoke(accountName) ?? "Couldn't update the block list.");

            if (_settings.IsBlockedAccount(accountName) && _input != null)
                _input.Text = string.Empty;

            Refresh(true);
        }

        private void Refresh(bool resetPage)
        {
            if (_list == null)
                return;

            var blockedAccounts = _settings.GetBlockedAccountNames().ToList();

            if (resetPage)
                _page.Reset();

            _page.Clamp(blockedAccounts.Count);
            _pageControls?.Update(blockedAccounts.Count);

            if (blockedAccounts.Count == 0)
            {
                _list.ShowEmptyMessage("No blocked accounts.");
                return;
            }

            _list.ClearRows();

            var pageRows = _page.GetPage(blockedAccounts);

            for (var index = 0; index < pageRows.Count; index++)
                AddRow(index, pageRows[index]);
        }

        private void AddRow(int index, string accountName)
        {
            var row = _list.AddRow(index, accountName);
            var rowWidth = _list.Width - 16;
            var buttonX = rowWidth - ActionButtonWidth - 8;

            _list.AddCell(row, accountName, 8, 3, buttonX - 16, Color.White);

            var unblockButton = new StandardButton
            {
                Text = "Unblock",
                Location = new Point(buttonX, 3),
                Size = new Point(ActionButtonWidth, 25),
                Parent = row
            };

            unblockButton.Click += (s, e) =>
            {
                SetStatus(_unblockAccount?.Invoke(accountName) ?? "Couldn't update the block list.");
                Refresh(false);
            };
        }

        private void SetStatus(string message)
        {
            if (_status != null)
                _status.Text = message ?? string.Empty;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _unwatchBlocks?.Invoke(OnBlocksChanged);
        }
    }
}
