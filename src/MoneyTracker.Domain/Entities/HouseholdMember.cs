namespace MoneyTracker.Domain.Entities;

public enum HouseholdMemberRole
{
    Owner = 0,
    Member = 1
}

public enum HouseholdMemberStatus
{
    Active = 0,
    Left = 1
}

/// <summary>
/// Khoảng thời gian một user là thành viên của household. KHÔNG xóa khi rời,
/// chỉ set LeftAt + Status=Left. Nếu rejoin → tạo row mới (nhiều khoảng).
/// Báo cáo gia đình filter theo [JoinedAt, LeftAt ?? now].
/// </summary>
public class HouseholdMember
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public HouseholdMemberRole Role { get; set; }
    public HouseholdMemberStatus Status { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }

    public Household? Household { get; set; }
    public User? User { get; set; }
    public ICollection<HouseholdCategoryShare> CategoryShares { get; set; } = new List<HouseholdCategoryShare>();
}
