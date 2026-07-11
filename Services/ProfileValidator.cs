using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using rp.spark.Models;

namespace rp.spark.Services
{
    public class ProfileValidator
    {
        public ProfileValidationResult Validate(CharacterProfile profile)
        {
            var result = new ProfileValidationResult();

            if (profile == null)
            {
                result.AddError("Profile missing!");
                return result;
            }

            if (profile.SchemaVersion != 1)
                result.AddError("Profile schema is invalid.");

            if (string.IsNullOrWhiteSpace(profile.ProfileName))
                result.AddError("Profile name is required.");

            if (profile.ProfileName?.Length > ProfileLimits.MaxProfileNameLength)
                result.AddError("Profile name is too long.");

            if (profile.CharacterName?.Length > ProfileLimits.MaxOfficialCharacterNameLength)
                result.AddError("Character name is too long.");

            if (profile.AccountName?.Length > ProfileLimits.MaxAccountNameLength)
                result.AddError("Account name is too long.");

            if (profile.DisplayName?.Length > ProfileLimits.MaxDisplayNameLength)
                result.AddError("Display name is too long.");

            if (profile.Pronouns?.Length > ProfileLimits.MaxPronounsLength)
                result.AddError("Pronouns are too long.");

            if (profile.Race?.Length > ProfileLimits.MaxRaceLength)
                result.AddError("Race is too long.");

            if (profile.CustomRace?.Length > ProfileLimits.MaxCustomRaceLength)
                result.AddError("Custom race is too long.");

            if (profile.Profession?.Length > ProfileLimits.MaxProfessionLength)
                result.AddError("Profession is too long.");

            if (profile.Specialization?.Length > ProfileLimits.MaxProfessionLength)
                result.AddError("Specialization is too long.");

            if (profile.CustomProfession?.Length > ProfileLimits.MaxProfessionLength)
                result.AddError("Custom profession is too long.");

            if (!Enum.IsDefined(typeof(ProfileExperience), profile.Experience))
                result.AddError("Experience selection is invalid.");

            if ((profile.Preferences & ~ProfilePreferenceFlags.All) != 0)
                result.AddError("Preferences selection is invalid.");

            if ((profile.Themes & ~ProfileThemeFlags.All) != 0)
                result.AddError("Themes selection is invalid.");

            if ((profile.Styles & ~ProfileStyleFlags.All) != 0)
                result.AddError("Styles selection is invalid.");

            if (profile.KnownFor?.Length > ProfileLimits.MaxKnownForLength)
                result.AddError("Known For is too long.");

            if (profile.Description?.Length > ProfileLimits.MaxDescriptionLength)
                result.AddError("Description is too long.");

            if (profile.Currently?.Length > ProfileLimits.MaxCurrentlyLength)
                result.AddError("Currently info is too long.");

            if (profile.OutOfCharacterInfo?.Length > ProfileLimits.MaxOutOfCharacterInfoLength)
                result.AddError("Other information is too long.");

            if (profile.AtAGlance != null && profile.AtAGlance.Count > ProfileLimits.MaxAtAGlanceEntries)
                result.AddError($"At a glance can contain {ProfileLimits.MaxAtAGlanceEntries} total entries. Please fix your JSON to remove extras from the file.");

            foreach (var entry in profile.AtAGlance ?? Enumerable.Empty<AtAGlanceEntry>())
            {
                if (entry == null)
                {
                    result.AddError("At a glance entries are null. Please delete the profile in your local files to fix.");
                    continue;
                }

                if (entry.AssetId <= 0)
                    result.AddError("At a glance icon asset IDs must be positive numbers!");

                if (entry.Title?.Length > ProfileLimits.MaxAtAGlanceTitleLength)
                    result.AddError("At a glance title is too long.");

                if (entry.Description?.Length > ProfileLimits.MaxAtAGlanceDescriptionLength)
                    result.AddError("At a glance description is too long.");

                if (entry.Tooltip?.Length > ProfileLimits.MaxTooltipLength)
                    result.AddError("At a glance tooltip is too long.");
            }

            return result;
        }
    }
}
