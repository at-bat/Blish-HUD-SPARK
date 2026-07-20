using System;
using System.Collections.Generic;
using System.Linq;

namespace rp.spark.Models
{
    internal static class ProfileDiscoveryMapper
    {
        public static ProfileDiscoveryTags FromProfile(CharacterProfile profile)
        {
            return profile == null
                ? new ProfileDiscoveryTags()
                : FromSelections(profile.Preferences, profile.Themes, profile.Styles, profile.DiscoveryTags);
        }

        public static ProfileDiscoveryTags FromSelections(
            ProfilePreferenceFlags preferences,
            ProfileThemeFlags themes,
            ProfileStyleFlags styles,
            ProfileDiscoveryTags discoveryTags = null)
        {
            var tags = Normalize(discoveryTags);

            tags.Preferences.AddRange(ProfileLabels.PreferenceOptions
                .Where(option => (preferences & option.Key) == option.Key)
                .Select(option => option.Key.ToString()));

            tags.Themes.AddRange(ProfileLabels.ThemeOptions
                .Where(option => (themes & option.Key) == option.Key)
                .Select(option => option.Key.ToString()));

            tags.Styles.AddRange(ProfileLabels.StyleOptions
                .Where(option => (styles & option.Key) == option.Key)
                .Select(option => option.Key.ToString()));

            return Normalize(tags);
        }

        public static ProfileDiscoveryTags Normalize(ProfileDiscoveryTags tags)
        {
            return new ProfileDiscoveryTags
            {
                Preferences = NormalizeValues(tags?.Preferences),
                Themes = NormalizeValues(tags?.Themes),
                Styles = NormalizeValues(tags?.Styles)
            };
        }

        public static ProfileDiscoveryTags Merge(params ProfileDiscoveryTags[] values)
        {
            var available = values ?? Array.Empty<ProfileDiscoveryTags>();

            return new ProfileDiscoveryTags
            {
                Preferences = NormalizeValues(available.SelectMany(value => value?.Preferences ?? Enumerable.Empty<string>())),
                Themes = NormalizeValues(available.SelectMany(value => value?.Themes ?? Enumerable.Empty<string>())),
                Styles = NormalizeValues(available.SelectMany(value => value?.Styles ?? Enumerable.Empty<string>()))
            };
        }

        public static bool AreEqual(ProfileDiscoveryTags left, ProfileDiscoveryTags right)
        {
            var normalizedLeft = Normalize(left);
            var normalizedRight = Normalize(right);

            return new HashSet<string>(normalizedLeft.Preferences, StringComparer.OrdinalIgnoreCase).SetEquals(normalizedRight.Preferences)
                && new HashSet<string>(normalizedLeft.Themes, StringComparer.OrdinalIgnoreCase).SetEquals(normalizedRight.Themes)
                && new HashSet<string>(normalizedLeft.Styles, StringComparer.OrdinalIgnoreCase).SetEquals(normalizedRight.Styles);
        }

        private static List<string> NormalizeValues(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Select(value => value?.Trim().ToLowerInvariant())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}