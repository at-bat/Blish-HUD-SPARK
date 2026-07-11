namespace rp.spark.Services
{
    // SPARK server matches these limits, so profiles sent to the server beyond these get rejected.
    public static class ProfileLimits
    {
        public const int MaxAtAGlanceEntries = 5;
        public const int MaxProfileNameLength = 30;
        public const int MaxDisplayNameLength = 30;
        public const int MaxPronounsLength = 20;
        public const int MaxTooltipLength = 160;
        public const int MaxAtAGlanceTitleLength = 60;
        public const int MaxAtAGlanceDescriptionLength = 280;
        public const int MaxKnownForLength = 500;
        public const int MaxDescriptionLength = 8000;
        public const int MaxCurrentlyLength = 500;
        public const int MaxOutOfCharacterInfoLength = 1000;
        public const int MaxReportReasonLength = 140;
        public const int MaxOfficialCharacterNameLength = 20;
        public const int MaxAccountNameLength = 30;
        public const int MaxRaceLength = 16;
        public const int MaxCustomRaceLength = 16;
        public const int MaxProfessionLength = 40;
    }
}
