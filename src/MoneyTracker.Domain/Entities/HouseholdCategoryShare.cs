using MoneyTracker.Domain.Common;

namespace MoneyTracker.Domain.Entities;

/// <summary>
/// Một share-rule cho 1 category, do 1 member khởi tạo trong 1 khoảng thời gian.
/// 
/// Theo yêu cầu: vừa share visibility (target thấy giao dịch trong report gia đình)
/// VỪA assign vào ví của target (target có thể dùng category này để ghi giao dịch).
/// 
/// SharedWithAllMembers=true → áp dụng cho tất cả member ACTIVE của household tại mỗi thời điểm.
/// SharedWithAllMembers=false → chỉ áp dụng cho các target được liệt kê trong Targets.
/// 
/// Time window: [StartedAt, EndedAt ?? now] giao với [member.JoinedAt, member.LeftAt ?? now]
/// của cả sharer và viewer khi compute report.
/// </summary>
public class HouseholdCategoryShare : IAuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Membership row của người share (chứa cả HouseholdId và sharer UserId).</summary>
    public Guid HouseholdMemberId { get; set; }

    /// <summary>Category được share (thuộc về sharer).</summary>
    public Guid CategoryId { get; set; }

    public bool SharedWithAllMembers { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public HouseholdMember? HouseholdMember { get; set; }
    public Category? Category { get; set; }
    public ICollection<HouseholdCategoryShareTarget> Targets { get; set; } = new List<HouseholdCategoryShareTarget>();
}

/// <summary>
/// Chỉ dùng khi share.SharedWithAllMembers = false.
/// Liệt kê các target members cụ thể.
/// </summary>
public class HouseholdCategoryShareTarget
{
    public Guid Id { get; set; }
    public Guid ShareId { get; set; }

    /// <summary>Membership row của target.</summary>
    public Guid TargetHouseholdMemberId { get; set; }

    public HouseholdCategoryShare? Share { get; set; }
    public HouseholdMember? TargetHouseholdMember { get; set; }
}
