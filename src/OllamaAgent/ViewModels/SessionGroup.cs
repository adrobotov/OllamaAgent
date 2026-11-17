using System.Collections.Generic;

namespace OllamaAgent.ViewModels;

/// <summary>
/// A group of sessions for date-based grouping in the sidebar.
/// </summary>
public class SessionGroup
{
    public string Name { get; }
    public List<Models.ChatSession> Sessions { get; }

    public SessionGroup(string name, List<Models.ChatSession> sessions)
    {
        Name = name;
        Sessions = sessions;
    }
}
