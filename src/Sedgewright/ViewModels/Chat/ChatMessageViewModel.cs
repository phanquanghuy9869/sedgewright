namespace Sedgewright.ViewModels.Chat;

public sealed class ChatMessageViewModel(string author, string content, bool isUser)
{
    public string Author { get; } = author;

    public string Content { get; } = content;

    public bool IsUser { get; } = isUser;

    public bool IsAssistant => !IsUser;

    public string AuthorLabel => Author;
}
