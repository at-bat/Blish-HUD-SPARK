using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models.Api;
using rp.spark.Services;
using rp.spark.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    internal sealed class RollGroupView : View
    {
        private const int ContentWidth = 760;
        private const int ContentHeight = 610;
        private const int LeftWidth = 430;
        private const int RightWidth = 310;
        private const int RowHeight = 30;
        private const int ButtonWidth = 85;

        private readonly RollGroupService _service;
        private readonly PageList _historyPage = new PageList(8);

        private Container _content;
        private Label _status;
        private Label _expiry;
        private CancellationTokenSource _clockCancel;
        private Task _clockTask;
        private string _shownGroupId;
        private long _shownRevision;
        private long _shownSequence;
        private bool _disbandArmed;
        private bool _unloaded;

        public RollGroupView(RollGroupService service)
        {
            _service = service;
        }

        protected override void Build(Container buildPanel)
        {
            _unloaded = false;

            _content = new Panel
            {
                Parent = buildPanel,
                Size = new Point(ContentWidth, ContentHeight)
            };

            _service.StateChanged += OnStateChanged;

            Render();
            _clockCancel = new CancellationTokenSource();
            _clockTask = RunClockAsync(_clockCancel.Token);
            RefreshOnOpen();
        }

        private async void RefreshOnOpen()
        {
            try
            {
                await _service.RefreshAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                SetStatus(
                    "Unable to refresh the roll group.");
            }
        }

        private void OnStateChanged()
        {
            SparkUiThread.Queue(() =>
            {
                if (_unloaded)
                    return;

                var group = _service.CurrentGroup;

                if (NeedsRender(group))
                    Render();
                else
                    SetStatus(_service.LastStatus);
            });
        }

        private bool NeedsRender(RollGroup group)
        {
            return !string.Equals(
                       _shownGroupId,
                       group?.GroupId ?? string.Empty,
                       StringComparison.Ordinal)
                || _shownSequence != (group?.LastSequence ?? 0)
                || _shownRevision != (group?.Revision ?? 0);
        }

        private void Render()
        {
            var group = _service.CurrentGroup;
            var groupId = group?.GroupId ?? string.Empty;

            if (!string.Equals(
                _shownGroupId ?? string.Empty,
                groupId,
                StringComparison.Ordinal))
            {
                _historyPage.Reset();
                _disbandArmed = false;
            }

            ClearContent();

            if (group == null)
                BuildNoGroup();
            else
                BuildGroup(group);

            _shownGroupId = groupId;
            _shownRevision = group?.Revision ?? 0;
            _shownSequence = group?.LastSequence ?? 0;
            SetStatus(_service.LastStatus);
        }

        private static void AddHeading(Container parent, string text, int width, int height, bool large = false)
        {
            SparkFormLayout.AddLabel(
                parent, text, width, height,
                large
                    ? GameService.Content.DefaultFont18
                    : GameService.Content.DefaultFont16,
                Color.White, true);
        }

        private void BuildNoGroup()
        {
            var stack = SparkFormLayout.AddVerticalStack(
                    _content,
                    90,
                    45,
                    580,
                    520,
                    10);

            var explanation = SparkFormLayout.AddLabel(
                    stack,
                    "Group up with others and use dice rolls! \nCreate a shared group for dice rolls, or join an existing group using its code." +
                    "\nUp to 50 people can join a group at a time.",
                    580,
                    58,
                    GameService.Content.DefaultFont16,
                    SparkViewUI.SecondaryTextColor);

            explanation.WrapText = true;

            AddHeading(stack, "Create a group", 580, 28);

            var createRow = SparkFormLayout.AddRow(stack, 580, RowHeight, 8);

            var createPassword = SparkFormLayout.AddTextBox(
                createRow, string.Empty, "Password (optional)",
                442, RowHeight, ProfileLimits.MaxRollPasswordLength);

            var createButton = SparkFormLayout.AddButton(createRow, "Create Group", 130, RowHeight);

            SparkUiActions.BindClick(
                createButton,
                async () =>
                    await _service.CreateAsync(
                        createPassword.Text),
                SetStatus,
                "Unable to create the group.");

            SparkFormLayout.AddSpacer(stack, 580, 12);

            AddHeading(stack, "Join an existing group", 580, 28);

            var joinRow = SparkFormLayout.AddRow(stack, 580, RowHeight, 8);

            var codeInput = SparkFormLayout.AddTextBox(
                    joinRow,
                    string.Empty,
                    "Group code",
                    155,
                    RowHeight,
                    5);

            var joinPassword = SparkFormLayout.AddTextBox(
                    joinRow,
                    string.Empty,
                    "Password (if needed)",
                    230,
                    RowHeight,
                    ProfileLimits.MaxRollPasswordLength);

            var joinButton = SparkFormLayout.AddButton(joinRow, "Join", 90, RowHeight);

            Task join() => _service.JoinAsync(codeInput.Text, joinPassword.Text);

            SparkUiActions.BindClick(joinButton, join, SetStatus, "Unable to join the group.");

            codeInput.EnterPressed += async (s, e) => await join();

            _status = AddStatus(stack, 580, 48);
        }

        private void BuildGroup(RollGroup group)
        {
            var columns = new FlowPanel
            {
                Parent = _content,
                Size = new Point(ContentWidth, ContentHeight),
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                ControlPadding = new Vector2(20, 0)
            };

            BuildRolls(columns, group);
            BuildMembers(columns, group);
        }

        private void BuildRolls(Container parent, RollGroup group)
        {
            var stack = SparkFormLayout.AddVerticalStack(parent, 0, 0, LeftWidth, ContentHeight, 8);

            AddHeading(stack, "Dice Rolling", LeftWidth, 30, true);

            var rollRow = SparkFormLayout.AddRow(stack, LeftWidth, RowHeight, 8);

            var rollInput = SparkFormLayout.AddTextBox(
                    rollRow,
                    string.Empty,
                    "Examples: d20, 1d8 + 4 #attack",
                    LeftWidth - 98,
                    RowHeight,
                    ProfileLimits.MaxRollInputLength);

            var rollButton = SparkFormLayout.AddButton(rollRow, "Roll", 90, RowHeight);

            SparkUiActions.BindClick(
                rollButton,
                () => SubmitRollAsync(rollInput),
                SetStatus,
                "Unable to submit the roll.");

            rollInput.EnterPressed += async (s, e) => await SubmitRollAsync(rollInput);

            AddHeading(stack, "History", LeftWidth, 26);

            var history = new ProfileScrollList(LeftWidth - 16, 350, 36)
                {
                    Parent = stack
                };

            var events = (group.History ?? new List<RollEvent>()).OrderByDescending(entry => entry.Sequence).ToList();

            _historyPage.Clamp(events.Count);

            if (events.Count == 0)
                history.ShowEmptyMessage("No activity yet.");

            var page = _historyPage.GetPage(events);

            for (var index = 0; index < page.Count; index++)
            {
                var entry = page[index];
                var isHeader = IsHeader(entry);

                var text = isHeader
                    ? $"— {HeaderText(entry)} —"
                    : FormatRoll(entry);

                var tooltip = isHeader
                    ? MakeHeaderTooltip(entry)
                    : MakeRollTooltip(entry);

                var row = history.AddRow(index, tooltip);
                var cell = history.AddCell(
                    row,
                    text,
                    8,
                    6,
                    LeftWidth - 40,
                    isHeader
                        ? new Color(255, 215, 100)
                        : Color.White);

                if (isHeader)
                    cell.HorizontalAlignment =
                        HorizontalAlignment.Center;

                cell.BasicTooltipText = null;
                cell.Tooltip = tooltip;
            }

            var pageControls = new PageListControls(stack, _historyPage, LeftWidth, () => SparkUiThread.Queue(Render));

            pageControls.Update(events.Count);

            _status = AddStatus(stack, LeftWidth, 42);
        }

        private Task SubmitRollAsync(TextBox input) => SubmitActivityAsync(input, text => _service.RollAsync(text));

        private Task SubmitHeaderAsync(TextBox input) => SubmitActivityAsync(input, text => _service.AddHeaderAsync(text));

        private static async Task SubmitActivityAsync(TextBox input, Func<string, Task<bool>> submit)
        {
            if (!await submit(input?.Text))
                return;

            SparkUiThread.Queue(() =>
            {
                if (input?.Parent != null)
                    input.Text = string.Empty;
            });
        }

        private void BuildMembers(Container parent, RollGroup group)
        {
            var stack = SparkFormLayout.AddVerticalStack(
                    parent,
                    0,
                    0,
                    RightWidth,
                    ContentHeight,
                    8);

            var headerRow = SparkFormLayout.AddRow(stack, RightWidth, RowHeight, 8);

            AddHeading(headerRow, $"Group {group.Code}", RightWidth - 108, RowHeight, true);

            var copyButton = SparkFormLayout.AddButton(headerRow, "Copy Code", 100, RowHeight);

            copyButton.Click += async (s, e) => await CopyCodeAsync();

            _expiry = SparkFormLayout.AddLabel(
                    stack,
                    string.Empty,
                    RightWidth,
                    24,
                    GameService.Content.DefaultFont12,
                    SparkViewUI.SecondaryTextColor);

            RefreshExpiry();

            var isOwner = group.IsOwner(_service.CurrentAccountName);

            if (isOwner)
                BuildOwnerSettings(stack, group);

            AddHeading(stack, $"Members ({group.Members?.Count ?? 0}/{ProfileLimits.MaxRollGroupMembers})", RightWidth, 26);

            var memberList = new ProfileScrollList(RightWidth - 16, isOwner ? 299 : 375, RowHeight)
                {
                    Parent = stack
                };

            var members = OrderedMembers(group);

            if (members.Count == 0)
                memberList.ShowEmptyMessage("No members.");

            for (var index = 0; index < members.Count; index++)
                AddMemberRow(memberList, group, members[index], index, isOwner);

            var exitText = isOwner ? (_disbandArmed ? "Confirm Disband" : "Disband Group") : "Leave Group";

            var exitButton = SparkFormLayout.AddButton(stack, exitText, 145, RowHeight);

            if (isOwner)
            {
                SparkUiActions.BindClick(exitButton, async () =>
                    {
                        if (!_disbandArmed)
                        {
                            _disbandArmed = true;
                            exitButton.Text = "Confirm Disband";

                            SetStatus("Click Confirm Disband to permanently close this group.");

                            return;
                        }

                        await _service.DisbandAsync();
                    },
                    SetStatus, "Unable to disband the group.");
            }
            else
            {
                SparkUiActions.BindClick(exitButton, async () => await _service.LeaveAsync(), SetStatus, "Unable to leave the group.");
            }
        }

        private void BuildOwnerSettings(FlowPanel stack, RollGroup group)
        {
            var allowNewMembers = SparkFormLayout.AddCheckbox(
                    stack,
                    "Allow new members",
                    !group.JoinLocked,
                    RightWidth,
                    RowHeight);

            Task Update(string password, bool clear) => _service.UpdateSettingsAsync(allowNewMembers.Checked, password, clear);

            allowNewMembers.CheckedChanged += async (s, e) => await Update(string.Empty, false);

            var passwordRow = SparkFormLayout.AddRow(stack, RightWidth, RowHeight, 6);

            var passwordInput = SparkFormLayout.AddTextBox(
                    passwordRow,
                    string.Empty,
                    group.HasPassword
                        ? "Change password"
                        : "Set password",
                    155,
                    RowHeight,
                    ProfileLimits.MaxRollPasswordLength);

            var setPassword = SparkFormLayout.AddButton(passwordRow, "Set", 62, RowHeight);

            SparkUiActions.BindClick(
                setPassword,
                () => Update(passwordInput.Text, false),
                SetStatus,
                "Unable to set the password.");

            var removePassword = SparkFormLayout.AddButton(
                    passwordRow,
                    "Remove",
                    75,
                    RowHeight,
                    group.HasPassword);

            SparkUiActions.BindClick(
                removePassword,
                () => Update(string.Empty, true),
                SetStatus,
                "Unable to remove the password.");

            var headerRow = SparkFormLayout.AddRow(
                stack,
                RightWidth,
                RowHeight,
                6);

            var headerInput = SparkFormLayout.AddTextBox(
                headerRow,
                string.Empty,
                "Add header",
                RightWidth - 98,
                RowHeight,
                ProfileLimits.MaxRollHeaderLength);

            var addHeader = SparkFormLayout.AddButton(
                headerRow,
                "Post",
                90,
                RowHeight);

            SparkUiActions.BindClick(
                addHeader,
                () => SubmitHeaderAsync(headerInput),
                SetStatus,
                "Unable to add the group header.");

            headerInput.EnterPressed += async (s, e) =>
                await SubmitHeaderAsync(headerInput);
        }

        private void AddMemberRow(
            ProfileScrollList list,
            RollGroup group,
            RollMember member,
            int index,
            bool isOwner)
        {
            var memberIsOwner = IsOwner(group, member);
            var name = FormatMember(member);
            var tooltip = MakeMemberTooltip(group, member);
            var row = list.AddRow(index, tooltip);
            var canKick = isOwner && !memberIsOwner;
            var nameWidth = canKick ? RightWidth - ButtonWidth - 42 : RightWidth - 34;

            var nameCell = list.AddCell(
                row,
                name,
                8,
                3,
                nameWidth,
                memberIsOwner
                    ? new Color(255, 215, 100)
                    : Color.White);

            nameCell.BasicTooltipText = null;
            nameCell.Tooltip = tooltip;

            if (!canKick)
                return;

            var kickButton = new StandardButton
            {
                Text = "Kick",
                Location = new Point(RightWidth - ButtonWidth - 24, 3),
                Size = new Point(ButtonWidth, 25),
                Parent = row
            };

            SparkUiActions.BindClick(
                kickButton,
                () => _service.KickAsync(member.AccountName),
                SetStatus,
                $"Unable to remove {member.AccountName}.");
        }

        private static List<RollMember> OrderedMembers(RollGroup group) =>
            (group?.Members ?? new List<RollMember>())
                .OrderByDescending(member => IsOwner(group, member))
                .ThenBy(member => member?.AccountName)
                .ToList();

        private static string FormatMember(RollMember member)
        {
            var account = member?.AccountName?.Trim() ?? "Unknown";
            var character = member?.CharacterName?.Trim() ?? string.Empty;

            return string.IsNullOrWhiteSpace(character)
                ? account
                : $"{account} [{character}]";
        }

        private static Tooltip MakeMemberTooltip(RollGroup group, RollMember member)
        {
            var account = member?.AccountName?.Trim() ?? "Unknown";

            var rolls = (group?.History ?? new List<RollEvent>())
                .Where(entry => IsRoll(entry)
                    && string.Equals(
                        entry.AccountName?.Trim(),
                        account,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.Sequence)
                .Take(3)
                .Select(FormatMemberRoll)
                .ToList();

            return new Tooltip(new ProfilePresenceTooltipView(
                member?.CharacterName?.Trim(), account,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                false, false, false, false, 3,
                additionalSectionTitle: rolls.Count > 0 ? "Recent Rolls" : string.Empty,
                additionalSectionText: string.Join("\n", rolls)));
        }

        private static bool IsRoll(RollEvent entry) =>
            string.Equals(
                entry?.Type,
                "roll",
                StringComparison.OrdinalIgnoreCase);

        private static string FormatMemberRoll(RollEvent entry)
        {
            var time = entry.Timestamp.ToLocalTime()
                .ToString("h:mmtt")
                .ToLowerInvariant();

            var tag = string.IsNullOrWhiteSpace(entry.Tag)
                ? string.Empty
                : $"[{entry.Tag.Trim()}] ";

            return $"{time} - {tag}{entry.Total}";
        }

        private static bool IsHeader(RollEvent entry) =>
            string.Equals(
                entry?.Type,
                "header",
                StringComparison.OrdinalIgnoreCase);

        private static string HeaderText(RollEvent entry) =>
            string.IsNullOrWhiteSpace(entry?.Text)
                ? "Group Header"
                : entry.Text.Trim();

        private static Tooltip MakeHeaderTooltip(
            RollEvent entry)
        {
            var account = entry?.AccountName?.Trim()
                ?? "Unknown";

            var character = entry?.CharacterName?.Trim();
            var author = string.IsNullOrWhiteSpace(character)
                ? account
                : character;

            var time = entry?.Timestamp
                .ToLocalTime()
                .ToString("h:mmtt")
                .ToLowerInvariant()
                ?? string.Empty;

            var details = $"[{time}] Posted by {author}";

            if (!string.IsNullOrWhiteSpace(character)
                && !string.IsNullOrWhiteSpace(account))
            {
                details += $"\nAccount: {account}";
            }

            return new Tooltip(new ProfileTooltipView(
                HeaderText(entry),
                details,
                "Group Header"));
        }

        private static string FormatRoll(RollEvent entry)
        {
            if (entry == null)
                return string.Empty;

            var name = string.IsNullOrWhiteSpace(entry.CharacterName) ? entry.AccountName : entry.CharacterName;

            var result = entry.Rolls == null || entry.Rolls.Count <= 1
                ? entry.Total.ToString()
                : $"[{string.Join(", ", entry.Rolls)}] = {entry.Total}";

            var time = entry.Timestamp.ToLocalTime().ToString("h:mmtt").ToLowerInvariant();

            var tag = string.IsNullOrWhiteSpace(entry.Tag) ? string.Empty : $"[{entry.Tag.Trim()}] ";

            return $"[{time}] {tag}{name} rolled {entry.Expression}: {result}";
        }

        private static Tooltip MakeRollTooltip(
            RollEvent entry)
        {
            var details = FormatRoll(entry);
            var account = entry?.AccountName?.Trim();

            if (!string.IsNullOrWhiteSpace(
                    entry?.CharacterName)
                && !string.IsNullOrWhiteSpace(account))
            {
                details += $"\nAccount: {account}";
            }

            var title = string.IsNullOrWhiteSpace(
                    entry?.Tag)
                ? $"Roll total: {entry?.Total ?? 0}"
                : $"{entry.Tag.Trim()} — " +
                  $"Roll total: {entry.Total}";

            return new Tooltip(new ProfileTooltipView(
                title,
                details,
                "Dice Roll"));
        }

        private async Task CopyCodeAsync()
        {
            var code = _service.CurrentGroup?.Code?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("No group code is available.");
                return;
            }

            const string failed = "Couldn't copy the group code right now.";
            string status;

            try
            {
                var copied = await ClipboardUtil.WindowsClipboardService
                    .SetTextAsync(code);

                status = copied ? $"Copied group code {code}." : failed;
            }
            catch
            {
                status = failed;
            }

            SparkUiThread.Queue(() => SetStatus(status));
        }

        private async Task RunClockAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                SparkUiThread.Queue(() =>
                {
                    if (!_unloaded)
                        RefreshExpiry();
                });
            }
        }

        private void RefreshExpiry()
        {
            if (_expiry == null)
                return;

            var group = _service.CurrentGroup;

            if (group == null)
            {
                _expiry.Text = string.Empty;

                return;
            }

            var remaining = group.ExpiresAt.ToUniversalTime() - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                _expiry.Text = "Group expired.";

                return;
            }

            _expiry.Text = $"Expires in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        }

        private static bool IsOwner(RollGroup group, RollMember member) =>
            string.Equals(member?.AccountName?.Trim(), group?.OwnerAccountName?.Trim(), StringComparison.OrdinalIgnoreCase);

        private void SetStatus(string message)
        {
            if (_status != null)
                _status.Text = message ?? string.Empty;
        }

        private static Label AddStatus(Container parent, int width, int height)
        {
            var label = SparkFormLayout.AddLabel(
                parent, string.Empty, width, height,
                GameService.Content.DefaultFont12,
                SparkViewUI.SecondaryTextColor);

            label.WrapText = true;
            return label;
        }

        private void ClearContent()
        {
            if (_content == null)
                return;

            foreach (var child in _content.Children.ToArray())
                child.Dispose();

            _status = null;
            _expiry = null;
        }

        protected override void Unload()
        {
            _unloaded = true;
            _service.StateChanged -= OnStateChanged;

            var cancellation = _clockCancel;
            var task = _clockTask;

            _clockCancel = null;
            _clockTask = null;

            if (cancellation != null)
            {
                cancellation.Cancel();
                TaskCleanup.DisposeWhenComplete(task, cancellation);
            }
        }
    }
}