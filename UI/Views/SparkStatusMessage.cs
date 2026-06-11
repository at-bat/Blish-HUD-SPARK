using Blish_HUD;
using Blish_HUD.Controls;
using rp.spark.Services;
using System;

namespace rp.spark.UI.Views
{
    internal sealed class SparkStatusMessage
    {
        private readonly SparkSettings _settings;
        private readonly Action _requestServerSync;

        private TextBox _input;
        private Label _count;
        private Label _saveStatus;

        public SparkStatusMessage(SparkSettings settings, Action requestServerSync)
        {
            _settings = settings;
            _requestServerSync = requestServerSync;
        }

        public void Build(FlowPanel settingsStack, int contentWidth)
        {
            var statusStack = SparkFormLayout.AddAutoStack(settingsStack, contentWidth, 2);

            SparkFormLayout.AddLabel(statusStack, "Status Message:", contentWidth, 24);

            var inputRow = SparkFormLayout.AddRow(statusStack, contentWidth, 35, 8);
            _input = SparkFormLayout.AddTextBox(
                inputRow,
                _settings.StatusMessage.Value,
                "Short message shown in the online list",
                contentWidth - 120,
                35,
                SparkSettings.MaxStatusLength);

            var submitButton = SparkFormLayout.AddButton(inputRow, "Submit", 112);
            submitButton.Click += (s, e) => SaveStatusMessage();
            _input.EnterPressed += (s, e) => SaveStatusMessage();
            _input.TextChanged += (s, e) => RefreshDraft();

            var statusRow = SparkFormLayout.AddRow(statusStack, contentWidth, 22, 8);
            _count = SparkFormLayout.AddLabel(
                statusRow,
                string.Empty,
                80,
                22,
                GameService.Content.DefaultFont12,
                SparkViewUI.SecondaryTextColor);

            _saveStatus = SparkFormLayout.AddLabel(
                statusRow,
                string.Empty,
                contentWidth - 88,
                22,
                GameService.Content.DefaultFont12,
                SparkViewUI.SecondaryTextColor);

            RefreshDraft();
        }

        private void SaveStatusMessage()
        {
            var statusMessage = _input?.Text?.Trim() ?? string.Empty;

            if (statusMessage.Length > SparkSettings.MaxStatusLength)
                statusMessage = statusMessage.Substring(0, SparkSettings.MaxStatusLength);

            _settings.StatusMessage.Value = statusMessage;

            if (_input != null && !string.Equals(_input.Text, statusMessage, StringComparison.Ordinal))
                _input.Text = statusMessage;

            _requestServerSync?.Invoke();
            RefreshDraft();

            if (_saveStatus != null)
                _saveStatus.Text = "Saved.";
        }

        private void RefreshDraft()
        {
            if (_input == null)
                return;

            var draftText = _input.Text ?? string.Empty;

            if (_count != null)
                _count.Text = CountText(draftText);

            if (_saveStatus != null)
                _saveStatus.Text = HasDraft()
                    ? "Unsaved changes."
                    : string.Empty;
        }

        private bool HasDraft()
        {
            return !string.Equals(
                _input?.Text?.Trim() ?? string.Empty,
                _settings.GetStatusMessage(),
                StringComparison.Ordinal);
        }

        private static string CountText(string statusMessage)
        {
            return $"{(statusMessage ?? string.Empty).Length}/{SparkSettings.MaxStatusLength}";
        }
    }
}
