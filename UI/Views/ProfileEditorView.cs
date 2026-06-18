using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using rp.spark.Models;
using rp.spark.Services;
using rp.spark.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace rp.spark.UI.Views
{
    // Multi-line text boxes currently do not wrap yet
    // https://github.com/blish-hud/Blish-HUD/pull/984
    // Won't touch this and wait for it to be supported
    public class ProfileEditorView : View
    {
        private const int EmptyGlanceAssetId = 0;
        private const int GlanceEditorWidth = 500;
        private const int GlanceEditorHeight = 595;
        private const int TextBoxWidth = 820;
        private const int ShortTextBoxWidth = 400;
        private const int GlanceSlotSize = 50;
        private const int GlancePadding = 8;
        private const int DescriptionHeight = 185;
        private const int GlanceY = 435;
        private const int IconSearchResultLimit = 50;
        private const int IconResultListWidth = 444;
        private const int IconResultListHeight = 260;
        private const int IconResultRowHeight = 42;
        private static readonly Color PopupBackground = new Color(38, 35, 32);
        private static readonly Color PopupPanelBackground = new Color(28, 27, 25);
        private static readonly Color PopupRowBackground = new Color(42, 40, 37);
        private static readonly Color PopupAlternateRowBackground = new Color(52, 49, 45);
        private static readonly Color PopupStatusText = new Color(210, 210, 210);
        private readonly ProfileEditorSession _session;
        private readonly IconIndexService _iconIndex;

        private Container _buildPanel;
        private Label _status;
        private TextBox _displayName;
        private TextBox _customProfession;
        private TextBox _pronouns;
        private TextBox _knownFor;
        private MultilineTextBox _description;
        private AssetIcon[] _glanceSlots;
        private Panel _glanceEditorPanel;
        private bool _isRefreshing;

        public ProfileEditorView(ProfileEditorSession session, IconIndexService iconIndex = null)
        {
            _session = session;
            _iconIndex = iconIndex;
        }

        protected override void Build(Container buildPanel)
        {
            _buildPanel = buildPanel;

            if (!_session.InitialState.CanEditProfile)
            {
                ProfileEditorUI.ShowUnavailableMessage(buildPanel);
                return;
            }

            BuildFields(buildPanel);

            ProfileEditorUI.AddLabel(buildPanel, "At a Glance", GlanceY);
            BuildGlance(buildPanel, GlanceY + 25);

            BuildFooter(buildPanel);
            _session.ProfileChanged += HandleProfileChanged;
            RefreshFromSession();
        }

        private void BuildFields(Container buildPanel)
        {
            var form = SparkFormLayout.AddVerticalStack(buildPanel, 0, 0, TextBoxWidth, GlanceY - 10, 10);

            _displayName = SparkFormLayout.AddLabeledTextBox(
                form,
                "Character Name",
                string.Empty,
                string.Empty,
                ShortTextBoxWidth,
                maxLength: ProfileLimits.MaxDisplayNameLength);

            _displayName.TextChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.DisplayName = _displayName.Text?.Trim() ?? string.Empty;
            };

            var professionRow = SparkFormLayout.AddRow(form, TextBoxWidth, 60, 30);

            _customProfession = SparkFormLayout.AddLabeledTextBox(
                professionRow,
                "Custom Profession",
                string.Empty,
                string.Empty,
                ShortTextBoxWidth,
                maxLength: ProfileLimits.MaxProfessionLength);

            _customProfession.TextChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.CustomProfession = _customProfession.Text?.Trim() ?? string.Empty;
            };

            _pronouns = SparkFormLayout.AddLabeledTextBox(
                professionRow,
                "Pronouns",
                string.Empty,
                string.Empty,
                220,
                maxLength: ProfileLimits.MaxPronounsLength);

            _pronouns.TextChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.Pronouns = _pronouns.Text?.Trim() ?? string.Empty;
            };

            _knownFor = SparkFormLayout.AddLabeledTextBox(
                form,
                "Known For",
                string.Empty,
                string.Empty,
                TextBoxWidth,
                maxLength: ProfileLimits.MaxKnownForLength);

            _knownFor.TextChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.KnownFor = _knownFor.Text?.Trim() ?? string.Empty;
            };

            _description = SparkFormLayout.AddLabeledMultilineTextBox(
                form,
                "Description",
                string.Empty,
                string.Empty,
                TextBoxWidth,
                DescriptionHeight,
                ProfileLimits.MaxDescriptionLength);

            _description.TextChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.Description = _description.Text?.Trim() ?? string.Empty;
            };
        }

        private void BuildFooter(Container buildPanel)
        {
            _status = ProfileEditorUI.AddSaveFooter(buildPanel, _session);
            _session.StatusChanged += HandleStatusChanged;
        }

        private void HandleStatusChanged(string statusText)
        {
            SparkUiThread.Queue(() =>
            {
                if (_status?.Parent != null)
                    _status.Text = statusText ?? string.Empty;
            });
        }

        private void HandleProfileChanged()
        {
            SparkUiThread.Queue(() =>
            {
                if (_displayName?.Parent != null)
                    RefreshFromSession();
            });
        }

        private void RefreshFromSession()
        {
            _isRefreshing = true;

            try
            {
                _displayName.Text = _session.Profile.DisplayName ?? string.Empty;
                _customProfession.Text = _session.Profile.CustomProfession ?? string.Empty;
                _pronouns.Text = _session.Profile.Pronouns ?? string.Empty;
                _knownFor.Text = _session.Profile.KnownFor ?? string.Empty;
                _description.Text = _session.Profile.Description ?? string.Empty;

                if (_glanceSlots != null)
                {
                    for (var i = 0; i < _glanceSlots.Length; i++)
                        RefreshGlance(i);
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void BuildGlance(Container buildPanel, int y)
        {
            _glanceSlots = new AssetIcon[_session.Glance.Length];

            for (var i = 0; i < _glanceSlots.Length; i++)
            {
                var slotIndex = i;

                _glanceSlots[i] = new AssetIcon
                {
                    Location = new Point(i * (GlanceSlotSize + GlancePadding), y),
                    Size = new Point(GlanceSlotSize, GlanceSlotSize),
                    Parent = buildPanel,
                    BackgroundColor = new Color(20, 20, 20, 180)
                };

                _glanceSlots[i].Click += (s, e) => EditGlance(slotIndex);
                RefreshGlance(slotIndex);
            }
        }

        private void RefreshGlance(int index)
        {
            var slot = _glanceSlots[index];
            var entry = _session.Glance[index];
            var assetId = entry.AssetId > 0 ? entry.AssetId : EmptyGlanceAssetId;

            slot.SetAssetId(assetId);
            slot.Tooltip = MakeGlanceTooltip(entry);
        }

        private static Tooltip MakeGlanceTooltip(AtAGlanceEntry entry)
        {
            return new Tooltip(new ProfileTooltipView(entry?.Title, entry?.Description, "At a Glance"));
        }

        private void EditGlance(int index)
        {
            CloseGlanceEditor();

            var entry = _session.Glance[index];
            var popupParent = _buildPanel ?? GameService.Graphics.SpriteScreen;

            _glanceEditorPanel = new Panel
            {
                ShowBorder = true,
                Title = "At a Glance",
                Size = new Point(GlanceEditorWidth, GlanceEditorHeight),
                Location = GetGlanceEditorLocation(popupParent),
                Parent = popupParent,
                BackgroundColor = PopupBackground,
                ClipsBounds = false,
                ZIndex = 100
            };

            var closeButton = new StandardButton
            {
                Text = "X",
                Location = new Point(GlanceEditorWidth - 32, -28),
                Size = new Point(24, 24),
                Parent = _glanceEditorPanel,
                ClipsBounds = false,
                ZIndex = 10011
            };

            closeButton.Click += (s, e) => CloseGlanceEditor();

            ProfileEditorUI.AddLabel(_glanceEditorPanel, "Icon ID", 10);

            var assetIdBox = new TextBox
            {
                Text = entry.AssetId > 0 ? entry.AssetId.ToString() : string.Empty,
                PlaceholderText = "Asset ID",
                Location = new Point(12, 35),
                Size = new Point(110, 30),
                Parent = _glanceEditorPanel
            };

            var selectedIcon = new AssetIcon
            {
                Location = new Point(132, 34),
                Size = new Point(32, 32),
                Parent = _glanceEditorPanel,
                BackgroundColor = new Color(20, 20, 20, 180)
            };
            UpdateIconPreview(assetIdBox, selectedIcon);

            ProfileEditorUI.AddLabel(_glanceEditorPanel, "Search Icons", 10, 180);

            var iconSearchBox = new TextBox
            {
                Text = string.Empty,
                PlaceholderText = "Search",
                Location = new Point(180, 35),
                Size = new Point(220, 30),
                Parent = _glanceEditorPanel
            };

            var searchButton = new StandardButton
            {
                Text = "Search",
                Location = new Point(410, 35),
                Size = new Point(62, 30),
                Parent = _glanceEditorPanel
            };

            var iconResultsList = new ProfileScrollList(
                IconResultListWidth,
                IconResultListHeight,
                IconResultRowHeight,
                2)
            {
                Location = new Point(12, 75),
                Parent = _glanceEditorPanel,
                BackgroundColor = PopupPanelBackground,
            };

            ProfileEditorUI.AddLabel(_glanceEditorPanel, "Title", 345);

            var titleBox = new TextBox
            {
                Text = entry.Title ?? string.Empty,
                PlaceholderText = "Title",
                MaxLength = ProfileLimits.MaxAtAGlanceTitleLength,
                Location = new Point(12, 370),
                Size = new Point(460, 30),
                Parent = _glanceEditorPanel
            };

            ProfileEditorUI.AddLabel(_glanceEditorPanel, "Description", 405);

            var descriptionBox = new MultilineTextBox
            {
                Text = entry.Description ?? string.Empty,
                PlaceholderText = "Description",
                MaxLength = ProfileLimits.MaxAtAGlanceDescriptionLength,
                Location = new Point(12, 430),
                Size = new Point(460, 55),
                Parent = _glanceEditorPanel
            };

            var confirmButton = new StandardButton
            {
                Text = "Confirm",
                Location = new Point(12, 505),
                Size = new Point(95, 30),
                Parent = _glanceEditorPanel
            };

            var clearButton = new StandardButton
            {
                Text = "Clear",
                Location = new Point(117, 505),
                Size = new Point(75, 30),
                Parent = _glanceEditorPanel
            };

            var status = new Label
            {
                Text = string.Empty,
                Location = new Point(202, 509),
                Size = new Point(270, 25),
                TextColor = PopupStatusText,
                Parent = _glanceEditorPanel
            };

            assetIdBox.TextChanged += (s, e) => UpdateIconPreview(assetIdBox, selectedIcon);
            iconSearchBox.EnterPressed += (s, e) => SearchIcons(
                iconSearchBox.Text,
                assetIdBox,
                selectedIcon,
                iconResultsList,
                status);
            searchButton.Click += (s, e) => SearchIcons(
                iconSearchBox.Text,
                assetIdBox,
                selectedIcon,
                iconResultsList,
                status);
            status.Text = "Press Enter or Search.";

            confirmButton.Click += (s, e) =>
            {
                var assetText = assetIdBox.Text?.Trim();

                if (string.IsNullOrWhiteSpace(assetText))
                {
                    _session.Glance[index].AssetId = 0;
                    _session.Glance[index].Title = string.Empty;
                    _session.Glance[index].Description = string.Empty;
                    _session.Glance[index].Tooltip = string.Empty;
                }
                else if (int.TryParse(assetText, out var assetId) && assetId > 0)
                {
                    _session.Glance[index].AssetId = assetId;
                    _session.Glance[index].Title = titleBox.Text?.Trim() ?? string.Empty;
                    _session.Glance[index].Description = descriptionBox.Text?.Trim() ?? string.Empty;
                    _session.Glance[index].Tooltip = string.Empty;
                }
                else
                {
                    status.Text = "Invalid ID.";
                    return;
                }

                RefreshGlance(index);
                CloseGlanceEditor();
            };

            clearButton.Click += (s, e) =>
            {
                _session.Glance[index].AssetId = 0;
                _session.Glance[index].Title = string.Empty;
                _session.Glance[index].Description = string.Empty;
                _session.Glance[index].Tooltip = string.Empty;
                RefreshGlance(index);
                CloseGlanceEditor();
            };
        }

        private void SearchIcons(
            string queryText,
            TextBox assetIdBox,
            AssetIcon selectedIcon,
            ProfileScrollList resultsList,
            Label status)
        {
            resultsList.ClearRows();

            if (_iconIndex == null)
            {
                status.Text = "Icon search unavailable.";
                return;
            }

            var query = queryText?.Trim() ?? string.Empty;

            if (query.Length < 2)
            {
                status.Text = _iconIndex.IsLoaded
                    ? $"{_iconIndex.EntryCount} icons ready."
                    : "Icon index loading.";
                return;
            }

            var results = _iconIndex.Search(query, IconSearchResultLimit);
            ShowIconResults(results, assetIdBox, selectedIcon, resultsList, status);
        }

        private void ShowIconResults(
            IReadOnlyList<Gw2IconSearchResult> results,
            TextBox assetIdBox,
            AssetIcon selectedIcon,
            ProfileScrollList resultsList,
            Label status)
        {
            resultsList.ClearRows();

            status.Text = results.Count == 0
                ? "No matches."
                : $"{results.Count} matches.";

            if (results.Count == 0)
            {
                resultsList.ShowEmptyMessage("No matching icons.");
                return;
            }

            for (var i = 0; i < results.Count; i++)
                AddIconSearchResult(resultsList, results[i], i, assetIdBox, selectedIcon, status);
        }

        private static void AddIconSearchResult(
            ProfileScrollList list,
            Gw2IconSearchResult result,
            int index,
            TextBox assetIdBox,
            AssetIcon selectedIcon,
            Label status)
        {
            var row = list.AddRow(index, string.Empty);
            row.BackgroundColor = index % 2 == 0
                ? PopupRowBackground
                : PopupAlternateRowBackground;

            var icon = new AssetIcon
            {
                Location = new Point(3, 2),
                Size = new Point(38, 38),
                Parent = row,
                BackgroundColor = new Color(20, 20, 20, 180)
            };
            icon.SetAssetId(result.AssetId);

            var nameLabel = new Label
            {
                Text = Shorten(result.Name, 48),
                Location = new Point(50, 9),
                Size = new Point(380, 26),
                Parent = row,
                TextColor = Color.White
            };

            row.Click += (s, e) => SelectIconSearchResult(result, assetIdBox, selectedIcon, status);
            icon.Click += (s, e) => SelectIconSearchResult(result, assetIdBox, selectedIcon, status);
            nameLabel.Click += (s, e) => SelectIconSearchResult(result, assetIdBox, selectedIcon, status);
        }

        private static void SelectIconSearchResult(
            Gw2IconSearchResult result,
            TextBox assetIdBox,
            AssetIcon selectedIcon,
            Label status)
        {
            assetIdBox.Text = result.AssetId.ToString();
            selectedIcon.SetAssetId(result.AssetId);

            status.Text = "Selected.";
        }

        private static void UpdateIconPreview(TextBox assetIdBox, AssetIcon selectedIcon)
        {
            if (selectedIcon == null)
                return;

            var text = assetIdBox.Text?.Trim();

            if (int.TryParse(text, out var assetId) && assetId > 0)
            {
                selectedIcon.SetAssetId(assetId);
                return;
            }

            selectedIcon.SetAssetId(0);
        }

        private static string Shorten(string value, int maxLength)
        {
            value = value?.Trim() ?? string.Empty;

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private void CloseGlanceEditor()
        {
            _glanceEditorPanel?.Dispose();
            _glanceEditorPanel = null;
        }

        protected override void Unload()
        {
            _session.StatusChanged -= HandleStatusChanged;
            _session.ProfileChanged -= HandleProfileChanged;
            CloseGlanceEditor();
        }

        private static Point GetGlanceEditorLocation(Container parent)
        {
            var parentSize = parent?.ContentRegion.Size ?? GameService.Graphics.SpriteScreen.Size;
            const int padding = 8;

            var x = (parentSize.X - GlanceEditorWidth) / 2;
            var y = Math.Min(36, Math.Max(padding, (parentSize.Y - GlanceEditorHeight) / 2));

            return new Point(Math.Max(padding, x), Math.Max(padding, y));
        }

    }
}
