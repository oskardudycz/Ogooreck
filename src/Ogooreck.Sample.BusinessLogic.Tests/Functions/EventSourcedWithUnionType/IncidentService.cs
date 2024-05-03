namespace Ogooreck.Sample.BusinessLogic.Tests.Functions.EventSourcedWithUnionType;
using static IncidentEvent;
using static IncidentCommand;

public record IncidentCommand
{
    public record LogIncident(
        Guid IncidentId,
        Guid CustomerId,
        Contact Contact,
        string Description,
        Guid LoggedBy
    ): IncidentCommand;

    public record CategoriseIncident(
        Guid IncidentId,
        IncidentCategory Category,
        Guid CategorisedBy
    ): IncidentCommand;

    public record PrioritiseIncident(
        Guid IncidentId,
        IncidentPriority Priority,
        Guid PrioritisedBy
    ): IncidentCommand;

    public record AssignAgentToIncident(
        Guid IncidentId,
        Guid AgentId
    ): IncidentCommand;

    public record RecordAgentResponseToIncident(
        Guid IncidentId,
        IncidentResponse.FromAgent Response
    ): IncidentCommand;

    public record RecordCustomerResponseToIncident(
        Guid IncidentId,
        IncidentResponse.FromCustomer Response
    ): IncidentCommand;

    public record ResolveIncident(
        Guid IncidentId,
        ResolutionType Resolution,
        Guid ResolvedBy
    ): IncidentCommand;

    public record AcknowledgeResolution(
        Guid IncidentId,
        Guid AcknowledgedBy
    ): IncidentCommand;

    public record CloseIncident(
        Guid IncidentId,
        Guid ClosedBy
    ): IncidentCommand;

    private IncidentCommand() { }
}

internal static class IncidentService
{
    public static IncidentLogged Handle(Func<DateTimeOffset> now, LogIncident command)
    {
        var (incidentId, customerId, contact, description, loggedBy) = command;

        return new IncidentLogged(incidentId, customerId, contact, description, loggedBy, now());
    }

    public static IncidentCategorised Handle(Func<DateTimeOffset> now, Incident current, CategoriseIncident command)
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (incidentId, incidentCategory, categorisedBy) = command;

        return new IncidentCategorised(incidentId, incidentCategory, categorisedBy, now());
    }

    public static IncidentPrioritised Handle(Func<DateTimeOffset> now, Incident current, PrioritiseIncident command)
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (incidentId, incidentPriority, prioritisedBy) = command;

        return new IncidentPrioritised(incidentId, incidentPriority, prioritisedBy, now());
    }

    public static AgentAssignedToIncident Handle(Func<DateTimeOffset> now, Incident current,
        AssignAgentToIncident command)
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (incidentId, agentId) = command;

        return new AgentAssignedToIncident(incidentId, agentId, now());
    }

    public static AgentRespondedToIncident Handle(
        Func<DateTimeOffset> now,
        Incident current,
        RecordAgentResponseToIncident command
    )
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (incidentId, response) = command;

        return new AgentRespondedToIncident(incidentId, response, now());
    }

    public static CustomerRespondedToIncident Handle(
        Func<DateTimeOffset> now,
        Incident current,
        RecordCustomerResponseToIncident command
    )
    {
        if (current.Status == IncidentStatus.Closed)
            throw new InvalidOperationException("Incident is already closed");

        var (incidentId, response) = command;

        return new CustomerRespondedToIncident(incidentId, response, now());
    }

    public static IncidentResolved Handle(
        Func<DateTimeOffset> now,
        Incident current,
        ResolveIncident command
    )
    {
        if (current.Status is IncidentStatus.Resolved or IncidentStatus.Closed)
            throw new InvalidOperationException("Cannot resolve already resolved or closed incident");

        if (current.HasOutstandingResponseToCustomer)
            throw new InvalidOperationException("Cannot resolve incident that has outstanding responses to customer");

        var (incidentId, resolution, resolvedBy) = command;

        return new IncidentResolved(incidentId, resolution, resolvedBy, now());
    }

    public static ResolutionAcknowledgedByCustomer Handle(
        Func<DateTimeOffset> now,
        Incident current,
        AcknowledgeResolution command
    )
    {
        if (current.Status is not IncidentStatus.Resolved)
            throw new InvalidOperationException("Only resolved incident can be acknowledged");

        var (incidentId, acknowledgedBy) = command;

        return new ResolutionAcknowledgedByCustomer(incidentId, acknowledgedBy, now());
    }

    public static IncidentClosed Handle(
        Func<DateTimeOffset> now,
        Incident current,
        CloseIncident command
    )
    {
        if (current.Status is not IncidentStatus.ResolutionAcknowledgedByCustomer)
            throw new InvalidOperationException("Only incident with acknowledged resolution can be closed");

        if (current.HasOutstandingResponseToCustomer)
            throw new InvalidOperationException("Cannot close incident that has outstanding responses to customer");

        var (incidentId, acknowledgedBy) = command;

        return new IncidentClosed(incidentId, acknowledgedBy, now());
    }
}
