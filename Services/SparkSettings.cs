using Blish_HUD.Settings;
using rp.spark.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace rp.spark.Services
{
    public class SparkSettings
    {
        private const string SharingSettingsKey = "profile-sharing";
        private const string DiscoverySettingsKey = "profile-discovery";
        private const string PrivacySettingsKey = "privacy";
        private const string UiSettingsKey = "ui";
        private const string BroadcastProfileKey = "BroadcastProfile";
        private const string AutoHideGameUiKey = "AutoHideGameUi";
        private const string HideLocationKey = "HideLocation";
        private const string CurrentStatusKey = "CurrentStatus";
        private const string RegionFilterKey = "RegionFilter";
        private const string BlockedAccountsKey = "BlockedAccounts";
        private const string AutoRefreshOnlineProfilesKey = "AutoRefreshOnlineProfiles";
        private const string ShowMatureProfilesKey = "ShowMatureProfiles";
        private const string ShowNearbyPresenceKey = "ShowNearbyPresence";
        private const string AutoRefreshNearbyRpersKey = "AutoRefreshNearbyRpers";

        private static readonly Regex AccountNameRegex = new Regex(@"^[^.\r\n]+\.\d{4}$", RegexOptions.Compiled);

        public SettingCollection SharingSettings { get; }
        public SettingCollection DiscoverySettings { get; }
        public SettingEntry<bool> ShowMatureProfiles { get; }
        public SettingCollection PrivacySettings { get; }
        public SettingCollection UiSettings { get; }
        public SettingEntry<bool> BroadcastProfile { get; }
        public SettingEntry<bool> HideLocation { get; }
        public SettingEntry<bool> AutoHideGameUi { get; }
        public SettingEntry<RPStatus> CurrentStatus { get; }
        public SettingEntry<ProfileRegion> RegionFilter { get; }
        public SettingEntry<bool> AutoRefreshOnlineProfiles { get; }
        public SettingEntry<string> BlockedAccounts { get; }
        public SettingEntry<bool> ShowNearbyPresence { get; }
        public SettingEntry<bool> AutoRefreshNearbyRpers { get; }

        public SparkSettings(SettingCollection settings)
        {
            SharingSettings = settings.AddSubCollection(
                SharingSettingsKey,
                true,
                () => "Profile sharing");

            DiscoverySettings = settings.AddSubCollection(
                DiscoverySettingsKey,
                true,
                () => "Profile discovery");

            ShowMatureProfiles = DiscoverySettings.DefineSetting(
                ShowMatureProfilesKey,
                false,
                () => "Show mature/18+ profiles",
                () => "Allows profiles marked as mature/18+ to appear in online and saved profile lists.");

            PrivacySettings = settings.AddSubCollection(
                PrivacySettingsKey,
                true,
                () => "Privacy");

            UiSettings = settings.AddSubCollection(
                UiSettingsKey,
                true,
                () => "Interface");

            BroadcastProfile = SharingSettings.DefineSetting(
                BroadcastProfileKey,
                false,
                () => "Share my profile online",
                () => "When enabled, SPARK will periodically publish your public profile to other players and show you on the online list.");

            HideLocation = SharingSettings.DefineSetting(
                HideLocationKey,
                false,
                () => "Hide my location",
                () => "When enabled, SPARK will show your location as Hidden instead of showing where you are.");

            ShowNearbyPresence = SharingSettings.DefineSetting(
                ShowNearbyPresenceKey,
                false,
                () => "Show me to nearby RPers",
                () => "When enabled, SPARK will publish your nearby RP presence to other opted-in SPARK users.");

            AutoHideGameUi = UiSettings.DefineSetting(
                AutoHideGameUiKey,
                true,
                () => "Auto-hide SPARK windows during in-game UI",
                () => "When enabled, SPARK closes profile windows during the fullscreen map and other GW2 UI states where overlays are not relevant.");

            CurrentStatus = SharingSettings.DefineSetting(
                CurrentStatusKey,
                RPStatus.Online,
                () => "Status",
                () => "Your RP status for profile sharing.");

            RegionFilter = DiscoverySettings.DefineSetting(
                RegionFilterKey,
                ProfileRegion.NA,
                () => "Region filter",
                () => "Filters online profiles to your chosen region.");

            AutoRefreshOnlineProfiles = DiscoverySettings.DefineSetting(
                AutoRefreshOnlineProfilesKey,
                true,
                () => "Auto-refresh online profiles",
                () => "Refreshes the open online profile list every 30 seconds.");

            AutoRefreshNearbyRpers = DiscoverySettings.DefineSetting(
                AutoRefreshNearbyRpersKey,
                true,
                () => "Auto-refresh nearby RPers",
                () => "Refreshes the nearby RPers window automatically.");

            BlockedAccounts = PrivacySettings.DefineSetting(
                BlockedAccountsKey,
                string.Empty,
                () => "Block list",
                () => "One account per line. Blocked accounts will be hidden from results.");
        }

        // Local filtering mirrors server block behaviour so cached profile lists stay filtered if server isn't available.
        // Cached bookmarks/recent entries are retained to avoid revealing whether another player blocked the current account or not.
        // By not removing these when you're blocked, you can't track who blocked you easily, etc.
        public IReadOnlyCollection<string> GetBlockedAccountNames()
        {
            return (BlockedAccounts.Value ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(account => account.Trim())
                .Where(IsValidAccountName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool AddBlockedAccount(string accountName)
        {
            if (!IsValidAccountName(accountName))
                return false;

            var accounts = GetBlockedAccountNames().ToList();

            if (accounts.Contains(accountName.Trim(), StringComparer.OrdinalIgnoreCase))
                return false;

            accounts.Add(accountName.Trim());
            SetBlockedAccountNames(accounts);
            return true;
        }

        public bool RemoveBlockedAccount(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return false;

            var accounts = GetBlockedAccountNames().ToList();
            var removed = accounts.RemoveAll(account => string.Equals(
                account,
                accountName.Trim(),
                StringComparison.OrdinalIgnoreCase)) > 0;

            if (removed)
                SetBlockedAccountNames(accounts);

            return removed;
        }

        public bool IsBlockedAccount(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return false;

            return GetBlockedAccountNames().Contains(accountName.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public void SetBlockedAccountNames(IEnumerable<string> accountNames)
        {
            var accounts = (accountNames ?? Enumerable.Empty<string>())
                .Select(account => account?.Trim() ?? string.Empty)
                .Where(IsValidAccountName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(account => account, StringComparer.OrdinalIgnoreCase)
                .ToList();

            BlockedAccounts.Value = string.Join(Environment.NewLine, accounts);
        }
        public string GetServerBaseUrl()
        {
            return SparkServiceConfig.ServerURL;
        }

        public static bool IsValidAccountName(string accountName)
        {
            return !string.IsNullOrWhiteSpace(accountName)
                && AccountNameRegex.IsMatch(accountName.Trim());
        }
    }
}
