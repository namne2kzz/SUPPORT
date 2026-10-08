using SUPPORT.Domain.Common;
using SUPPORT.Domain.Entities;

namespace SUPPORT.UnitTests.Domain;

public sealed class ConversationTests
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Start_LongQuestion_TruncatesTitle()
    {
        var conversation = Conversation.Start("dashboard", OrgId, UserId, new string('a', 100));

        conversation.Title.Length.ShouldBe(61);
        conversation.Title.ShouldEndWith("…");
    }

    [Fact]
    public void Start_EmptyQuestion_Throws()
    {
        Should.Throw<DomainException>(() => Conversation.Start("dashboard", OrgId, UserId, "  "));
    }

    [Fact]
    public void IsOwnedBy_OtherProductOrDeleted_ReturnsFalse()
    {
        var conversation = Conversation.Start("dashboard", OrgId, UserId, "Làm sao đóng sprint?");

        conversation.IsOwnedBy("dashboard", OrgId, UserId).ShouldBeTrue();
        conversation.IsOwnedBy("hub", OrgId, UserId).ShouldBeFalse();

        conversation.SoftDelete();
        conversation.IsOwnedBy("dashboard", OrgId, UserId).ShouldBeFalse();
    }
}
