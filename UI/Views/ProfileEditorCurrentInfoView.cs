using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using rp.spark.Models;
using rp.spark.Services;

namespace rp.spark.UI.Views
{
    public class ProfileEditorCurrentInfoView : View
    {
        private const int TextBoxWidth = 760;
        private const int FormHeight = 500;
        private const int SaveY = 515;
        private const int StatusY = 520;
        private const int ContextY = 560;

        private readonly ProfileEditorSession _session;

        private Label _status;
        private MultilineTextBox _currently;
        private MultilineTextBox _outOfCharacterInfo;
        private bool _isRefreshing;

        public ProfileEditorCurrentInfoView(ProfileEditorSession session)
        {
            _session = session;
        }

        protected override void Build(Container buildPanel)
        {
            if (!_session.State.CanEditProfile)
            {
                ProfileEditorUI.ShowUnavailableMessage(buildPanel);
                return;
            }

            var form = SparkFormLayout.AddVerticalStack(buildPanel, 0, 0, TextBoxWidth, FormHeight, 20);

            _currently = SparkFormLayout.AddLabeledMultilineTextBox(
                form,
                "Currently",
                string.Empty,
                "What is your character doing right now?",
                TextBoxWidth,
                180,
                ProfileLimits.MaxCurrentlyLength);

            _currently.TextChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.Currently = _currently.Text?.Trim() ?? string.Empty;
            };

            _outOfCharacterInfo = SparkFormLayout.AddLabeledMultilineTextBox(
                form,
                "Other information (out of character)",
                string.Empty,
                "OOC notes, contact preferences, boundaries, or scheduling info.",
                TextBoxWidth,
                225,
                ProfileLimits.MaxOutOfCharacterInfoLength);

            _outOfCharacterInfo.TextChanged += (s, e) =>
            {
                if (!_isRefreshing)
                    _session.Profile.OutOfCharacterInfo = _outOfCharacterInfo.Text?.Trim() ?? string.Empty;
            };

            BuildFooter(buildPanel);
            _session.ProfileChanged += HandleProfileChanged;
            RefreshFromSession();
        }

        private void BuildFooter(Container buildPanel)
        {
            _status = ProfileEditorUI.AddSaveFooter(buildPanel, _session, SaveY, StatusY, ContextY);
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
                if (_currently?.Parent != null)
                    RefreshFromSession();
            });
        }

        private void RefreshFromSession()
        {
            _isRefreshing = true;

            try
            {
                _currently.Text = _session.Profile.Currently ?? string.Empty;
                _outOfCharacterInfo.Text = _session.Profile.OutOfCharacterInfo ?? string.Empty;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        protected override void Unload()
        {
            _session.StatusChanged -= HandleStatusChanged;
            _session.ProfileChanged -= HandleProfileChanged;
        }

    }
}
