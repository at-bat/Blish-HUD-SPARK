using System;
using System.Collections.Generic;

namespace rp.spark.Models.Api
{
    public class RollGroup
    {
        public string GroupId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string OwnerAccountName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow;
        public bool JoinLocked { get; set; }
        public bool HasPassword { get; set; }
        public long Revision { get; set; }
        public long LastSequence { get; set; }
        public List<RollMember> Members { get; set; } = new List<RollMember>();
        public List<RollEvent> History { get; set; } = new List<RollEvent>();

        public bool IsOwner(string accountName)
        {
            return !string.IsNullOrWhiteSpace(accountName)
                && string.Equals(
                    OwnerAccountName?.Trim(),
                    accountName.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    public class RollMember
    {
        public string AccountName { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public class RollEvent
    {
        public long Sequence { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string AccountName { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public string Type { get; set; } = "roll";
        public string Expression { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<int> Rolls { get; set; } = new List<int>();
        public int Modifier { get; set; }
        public int Total { get; set; }
    }

    public class RollGroupResponse
    {
        public RollGroup Group { get; set; }
    }

    public class RollEventListResponse
    {
        public long Revision { get; set; }
        public long LastSequence { get; set; }
        public bool GroupChanged { get; set; }
        public List<RollEvent> Events { get; set; } = new List<RollEvent>();
    }

    public class RollEventResponse
    {
        public RollEvent Event { get; set; }
    }

    public class RollCharacterRequest
    {
        public string CharacterName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class CreateRollGroupRequest : RollCharacterRequest
    {
        public string Password { get; set; } = string.Empty;
    }

    public class JoinRollGroupRequest : RollCharacterRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RollGroupSettingsRequest
    {
        public bool JoinLocked { get; set; }
        public string NewPassword { get; set; } = string.Empty;
        public bool ClearPassword { get; set; }
    }

    public class RollMemberUpdateRequest : RollCharacterRequest
    {
    }

    public class RollRequest : RollCharacterRequest
    {
        public string Expression { get; set; } = string.Empty;
    }

    public class RollHeaderRequest
    {
        public string Text { get; set; } = string.Empty;
    }

    public class RollGroupActionRequest
    {
    }
}