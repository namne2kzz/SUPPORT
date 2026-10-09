namespace SUPPORT.IntegrationTests.Fakes;

/// <summary>A temporary knowledge folder with a few realistic documents; deleted on dispose.</summary>
public sealed class KnowledgeFolder : IDisposable
{
    /// <summary>Sprint guide.</summary>
    public const string Sprints = """
        ---
        key: guides/sprints
        title: Quản lý Sprint
        module: sprints
        route: /sprint-planning
        suggestions:
          - Làm sao đóng sprint?
          - Task chưa xong khi đóng sprint đi đâu?
        ---
        ## Tạo sprint
        Vào màn hình Sprint Planning, bấm **New Sprint**, nhập tên, ngày bắt đầu và ngày kết thúc.

        ## Đóng sprint
        Bấm **Close Sprint** khi sprint kết thúc.

        ### Task chưa xong
        Khi đóng sprint, các task chưa Done sẽ được chuyển về backlog để lên kế hoạch lại.
        """;

    /// <summary>Backlog guide.</summary>
    public const string Backlog = """
        ---
        key: guides/backlog
        title: Quản lý Backlog
        module: backlog
        route: /backlog
        suggestions:
          - Promote to Sprint là gì?
        ---
        ## Đưa item vào sprint
        Chọn item rồi bấm **Promote to Sprint** để đưa user story vào sprint đang mở.

        ## Sắp xếp ưu tiên
        Kéo thả item để thay đổi thứ tự ưu tiên trong backlog.
        """;

    /// <summary>General FAQ.</summary>
    public const string Faq = """
        ---
        key: faq
        title: Câu hỏi thường gặp
        module: general
        type: faq
        suggestions:
          - Tôi quên mật khẩu thì làm sao?
        ---
        ## Quên mật khẩu
        Bấm **Quên mật khẩu** ở màn hình đăng nhập và làm theo email hướng dẫn.
        """;

    /// <summary>Creates the folder with the three documents.</summary>
    public KnowledgeFolder()
    {
        Directory.CreateDirectory(Path.Combine(Root, "guides"));
        Write("guides/sprints.md", Sprints);
        Write("guides/backlog.md", Backlog);
        Write("faq.md", Faq);
    }

    /// <summary>Folder path.</summary>
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "support-knowledge-" + Guid.NewGuid().ToString("N"));

    /// <summary>Writes (or overwrites) a file relative to <see cref="Root"/>.</summary>
    /// <param name="relativePath">Relative path.</param>
    /// <param name="content">File content.</param>
    public void Write(string relativePath, string content) =>
        File.WriteAllText(Path.Combine(Root, relativePath), content);

    /// <summary>Deletes a file relative to <see cref="Root"/>.</summary>
    /// <param name="relativePath">Relative path.</param>
    public void Delete(string relativePath) => File.Delete(Path.Combine(Root, relativePath));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
