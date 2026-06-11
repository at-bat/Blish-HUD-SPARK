namespace rp.spark.Services
{
    public class PlayerState
    {
        public bool IsMumbleAvailable { get; set; }

        public bool IsInGame { get; set; }

        public string OfficialCharacterName { get; set; } = string.Empty;

        public string Race { get; set; } = string.Empty;

        public string Profession { get; set; } = string.Empty;

        public string Specialization { get; set; } = string.Empty;

        public int MapId { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public string AccountName { get; set; } = string.Empty;

        public bool IsCharacterApiVerified { get; set; }

        public bool HasCharactersPermission { get; set; }

        public bool CanEditProfile => IsMumbleAvailable && IsInGame && !string.IsNullOrWhiteSpace(OfficialCharacterName);
    }
}
