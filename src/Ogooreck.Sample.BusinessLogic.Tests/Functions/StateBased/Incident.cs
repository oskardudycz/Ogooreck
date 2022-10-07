namespace Ogooreck.Sample.BusinessLogic.Tests.Functions.StateBased;

public enum IncidentStatus
{
    Pending = 1,
    Resolved = 8,
    ResolutionAcknowledgedByCustomer = 16,
    Closed = 32
}

public record Resolution(
    Guid By,
    DateTimeOffset At,
    ResolutionType Type
);

public record Acknowledgement(
    Guid By,
    DateTimeOffset At
);

public record Closure(
    Guid By,
    DateTimeOffset At
);

public record Incident(
    Guid Id,
    Guid CustomerId,
    Contact Contact,
    Guid loggedBy,
    DateTimeOffset loggedAt,
    string? Description = null,
    IncidentStatus Status = IncidentStatus.Pending,
    bool HasOutstandingResponseToCustomer = false,
    IncidentCategory? Category = null,
    IncidentPriority? Priority = null,
    Guid? AgentId = null,
    IncidentResponse[]? Responses = null,
    Resolution? Resolved = null,
    Acknowledgement? Acknowledged = null,
    Closure? Closed = null,
    int Version = 1
);

public enum IncidentCategory
{
    Software,
    Hardware,
    Network,
    Database
}

public enum IncidentPriority
{
    Critical,
    High,
    Medium,
    Low
}

public enum ResolutionType
{
    Temporary,
    Permanent,
    NotAnIncident
}

public enum ContactChannel
{
    Email,
    Phone,
    InPerson,
    GeneratedBySystem
}

public record Contact(
    ContactChannel ContactChannel,
    string? FirstName = null,
    string? LastName = null,
    string? EmailAddress = null,
    string? PhoneNumber = null
);

public abstract record IncidentResponse
{
    public record FromAgent(
        Guid AgentId,
        string Content,
        bool VisibleToCustomer
    ): IncidentResponse(Content);

    public record FromCustomer(
        Guid CustomerId,
        string Content
    ): IncidentResponse(Content);

    public string Content { get; init; }

    private IncidentResponse(string content)
    {
        Content = content;
    }
}
