namespace MTGAAccessibility.Core.Interfaces
{
    public interface INavigable
    {
        string Name { get; }
        string Description { get; }
        bool IsEnabled { get; }
        string GetAnnouncementText();
    }
}
