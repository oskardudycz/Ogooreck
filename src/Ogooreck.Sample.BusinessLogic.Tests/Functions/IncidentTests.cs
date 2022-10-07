using Ogooreck.BusinessLogic;

namespace Ogooreck.Sample.BusinessLogic.Functions;

using static IncidentEventsBuilder;
using static IncidentService;

public class IncidentTests
{
    private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    private static readonly Func<Incident, object, Incident> evolve =
        (incident, @event) =>
        {
            return @event switch
            {
                IncidentLogged logged => Incident.Create(logged),
                IncidentCategorised categorised => incident.Apply(categorised),
                IncidentPrioritised prioritised => incident.Apply(prioritised),
                AgentRespondedToIncident agentResponded => incident.Apply(agentResponded),
                CustomerRespondedToIncident customerResponded => incident.Apply(customerResponded),
                IncidentResolved resolved => incident.Apply(resolved),
                ResolutionAcknowledgedByCustomer acknowledged => incident.Apply(acknowledged),
                IncidentClosed closed => incident.Apply(closed),
                _ => incident
            };
        };

    private readonly HandlerSpecification<Incident> Spec = Specification.For(evolve);

    [Fact]
    public void GivenNonExistingIncident_WhenOpenWithValidParams_ThenSucceeds()
    {
        var incidentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var contact = new Contact(ContactChannel.Email, EmailAddress: "john@doe.com");
        var description = Guid.NewGuid().ToString();
        var loggedBy = Guid.NewGuid();

        Spec.Given()
            .When(() => Handle(() => now, new LogIncident(incidentId, customerId, contact, description, loggedBy)))
            .Then(new IncidentLogged(incidentId, customerId, contact, description, loggedBy, now));
    }

    [Fact]
    public void GivenOpenIncident_WhenCategoriseWithValidParams_ThenSucceeds()
    {
        var incidentId = Guid.NewGuid();

        var category = IncidentCategory.Database;
        var categorisedBy = Guid.NewGuid();

        Spec.Given(IncidentLogged(incidentId, now))
            .When(incident => Handle(() => now, incident, new CategoriseIncident(incidentId, category, categorisedBy)))
            .Then(new IncidentCategorised(incidentId, category, categorisedBy, now));
    }
}

public static class IncidentEventsBuilder
{
    public static IncidentLogged IncidentLogged(Guid incidentId, DateTimeOffset now)
    {
        var customerId = Guid.NewGuid();
        var contact = new Contact(ContactChannel.Email, EmailAddress: "john@doe.com");
        var description = Guid.NewGuid().ToString();
        var loggedBy = Guid.NewGuid();

        return new IncidentLogged(incidentId, customerId, contact, description, loggedBy, now);
    }
}
