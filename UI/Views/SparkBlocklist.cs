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
        private const int ListHeight = 220;
        private const int RowHeight = 30;

        private readonly SparkSettings _settings;
        private readonly Func<string, string> _blockAccount;
        private readonly Func<string, string> _unblockAccount;
        private readonly Action<Action> _watchBlocks;
        private readonly Action<Action> _unwatchBlocks;

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

        public void Build(FlowPanel settingsStack, int contentWidth)
        {
            var blockedStack = SparkFormLayout.AddAutoStack(settingsStack, contentWidth, 5);
            var inputRow = SparkFormLayout.AddRow(blockedStack, contentWidth, 35, 8);

            _input = SparkFormLayout.AddTextBox(
                inputRow,
                string.Empty,
                "Account name, such as Name.1234",
                contentWidth - 120,
                35,
                ProfileLimits.MaxAccountNameLength);

            var addButton = SparkFormLayout.AddButton(inputRow, "Block", 112);
            addButton.Click += (s, e) => AddBlock();
            _input.EnterPressed += (s, e) => AddBlock();

            _list = new ProfileScrollList(contentWidth - 16, ListHeight, RowHeight)
            {
                Parent = blockedStack
            };

            _status = SparkFormLayout.AddLabel(
                blockedStack,
                string.Empty,
                contentWidth,
                24,
                GameService.Content.DefaultFont12,
                SparkViewUI.SecondaryTextColor);

            _watchBlocks?.Invoke(OnBlocksChanged);
            Refresh();
        }

        private void OnBlocksChanged()
        {
            GameService.Overlay.QueueMainThreadUpdate(gameTime =>
            {
                if (!_isDisposed)
                    Refresh();
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

            Refresh();
        }

        private void Refresh()
        {
            if (_list == null)
                return;

            var blockedAccounts = _settings.GetBlockedAccountNames().ToList();

            if (!blockedAccounts.Any())
            {
                _list.ShowEmptyMessage("No blocked accounts.");
                return;
            }

            _list.ClearRows();

            for (var i = 0; i < blockedAccounts.Count; i++)
                AddRow(i, blockedAccounts[i]);
        }

        private void AddRow(int index, string accountName)
        {
            var row = _list.AddRow(index, accountName);
            _list.AddCell(row, accountName, 8, 3, 500, Color.White);

            var unblockButton = new StandardButton
            {
                Text = "Unblock",
                Location = new Point(548, 3),
                Size = new Point(90, 25),
                Parent = row
            };

            unblockButton.Click += (s, e) =>
            {
                SetStatus(_unblockAccount?.Invoke(accountName) ?? "Couldn't update the block list.");
                Refresh();
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
