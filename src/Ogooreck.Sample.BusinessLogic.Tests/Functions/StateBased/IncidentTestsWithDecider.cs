using Ogooreck.BusinessLogic;

namespace Ogooreck.Sample.BusinessLogic.Tests.Functions.StateBased;

using static IncidentEventsBuilder;
using static IncidentService;

public class IncidentTestsWithDecider
{
    private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    private readonly DeciderSpecification<Incident> Spec = Specification.For(decide);

    private static Func<object, Incident, DecideResult<object, Incident>> decide => (command, currentState) =>
        DecideResult.For(
            command switch
            {
                LogIncident logIncident => Handle(() => now, logIncident),
                CategoriseIncident categorise => Handle(() => now, currentState, categorise),
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
            }
        );

    [Fact]
    public void GivenNonExistingIncident_WhenOpenWithValidParams_ThenSucceeds()
    {
        var incidentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var contact = new Contact(ContactChannel.Email, EmailAddress: "john@doe.com");
        var description = Guid.NewGuid().ToString();
        var loggedBy = Guid.NewGuid();

        Spec.Given()
            .When(new LogIncident(incidentId, customerId, contact, description, loggedBy))
            .Then(new Incident(incidentId, customerId, contact, loggedBy, now, description));
    }

    [Fact]
    public void GivenOpenIncident_WhenCategoriseWithValidParams_ThenSucceeds()
    {
        var incidentId = Guid.NewGuid();
        var loggedIncident = LoggedIncident(incidentId, now);

        var category = IncidentCategory.Database;
        var categorisedBy = Guid.NewGuid();

        Spec.Given(loggedIncident)
            .When(new CategoriseIncident(incidentId, category, categorisedBy))
            .Then(loggedIncident with { Category = category });
    }
}
