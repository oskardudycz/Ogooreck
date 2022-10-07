namespace Ogooreck.Sample.BusinessLogic.Tests.Functions.StateBased;

public record LogIncident(
    Guid IncidentId,
    Guid CustomerId,
    Contact Contact,
    string Description,
    Guid LoggedBy
);

public record CategoriseIncident(
    Guid IncidentId,
    IncidentCategory Category,
    Guid CategorisedBy
);

public record PrioritiseIncident(
    Guid IncidentId,
    IncidentPriority Priority,
    Guid PrioritisedBy
);

public record AssignAgentToIncident(
    Guid IncidentId,
    Guid AgentId
);

public record RecordAgentResponseToIncident(
    Guid IncidentId,
    IncidentResponse.FromAgent Response
);

public record RecordCustomerResponseToIncident(
    Guid IncidentId,
    IncidentResponse.FromCustomer Response
);

public record ResolveIncident(
    Guid IncidentId,
    ResolutionType Resolution,
    Guid ResolvedBy
);

public record AcknowledgeResolution(
    Guid IncidentId,
    Guid AcknowledgedBy
);

public record CloseIncident(
    Guid IncidentId,
    Guid ClosedBy
);

internal static class IncidentService
{
    public static Incident Handle(Func<DateTimeOffset> now, LogIncident command)
    {
        var (incidentId, customerId, contact, description, loggedBy) = command;

        return new Incident(incidentId, customerId,  contact, loggedBy, now(), description);
    }

    public static Incident Handle(Func<DateTimeOffset> now, Incident current, CategoriseIncident command)
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (_, category, _) = command;

        return current with { Category = category };
    }

    public static Incident Handle(Func<DateTimeOffset> now, Incident current, PrioritiseIncident command)
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (_, priority, _) = command;

        return current with { Priority = priority };
    }

    public static Incident Handle(Func<DateTimeOffset> now, Incident current, AssignAgentToIncident command)
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (_, agentId) = command;

        return current with { AgentId = agentId };
    }

    public static Incident Handle(
        Func<DateTimeOffset> now,
        Incident current,
        RecordAgentResponseToIncident command
    )
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (_, response) = command;

        return current with
        {
            Responses = (current.Responses ?? Array.Empty<IncidentResponse>()).Union(new[] { response }).ToArray()
        };
    }

    public static Incident Handle(
        Func<DateTimeOffset> now,
        Incident current,
        RecordCustomerResponseToIncident command
    )
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (_, response) = command;

        return current with
        {
            Responses = (current.Responses ?? Array.Empty<IncidentResponse>()).Union(new[] { response }).ToArray()
        };
    }

    public static Incident Handle(
        Func<DateTimeOffset> now,
        Incident current,
        ResolveIncident command
    )
    {
        if (current.Status is IncidentStatus.Resolved or IncidentStatus.Closed)
            throw new InvalidOperationException("Cannot resolve already resolved or closed incident");

        if (current.HasOutstandingResponseToCustomer)
            throw new InvalidOperationException("Cannot resolve incident that has outstanding responses to customer");

        var (_, resolution, resolvedBy) = command;

        return current with
        {
            Status = IncidentStatus.ResolutionAcknowledgedByCustomer,
            Resolved = new Resolution(resolvedBy, now(), resolution)
        };
    }

    public static Incident Handle(
        Func<DateTimeOffset> now,
        Incident current,
        AcknowledgeResolution command
    )
    {
        if (current.Status is not IncidentStatus.Resolved)
            throw new InvalidOperationException("Only resolved incident can be acknowledged");

        var (_, acknowledgedBy) = command;

        return current with
        {
            Status = IncidentStatus.ResolutionAcknowledgedByCustomer,
            Acknowledged = new Acknowledgement(acknowledgedBy, now())
        };
    }

    public static Incident Handle(
        Func<DateTimeOffset> now,
        Incident current,
        CloseIncident command
    )
    {
        if (current.Status is not IncidentStatus.ResolutionAcknowledgedByCustomer)
            throw new InvalidOperationException("Only incident with acknowledged resolution can be closed");

        if (current.HasOutstandingResponseToCustomer)
            throw new InvalidOperationException("Cannot close incident that has outstanding responses to customer");

        var (_, closedBy) = command;

        return current with { Status = IncidentStatus.Closed, Closed = new Closure(closedBy, now()) };
    }
}
