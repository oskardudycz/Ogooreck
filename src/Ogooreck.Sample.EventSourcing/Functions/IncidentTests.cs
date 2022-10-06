// using System.Reflection.Metadata;
// using Ogooreck.BusinessLogic;
// using Ogooreck.Sample.EventSourcing.Aggregates;
// using Ogooreck.Sample.EventSourcing.Aggregates.Core;
// using Ogooreck.Sample.EventSourcing.Aggregates.Pricing;
// using Ogooreck.Sample.EventSourcing.Aggregates.Products;
//
// namespace Ogooreck.Sample.EventSourcing.Functions;
//
// using static IncidentEventsBuilder;
// using static ProductItemBuilder;
//
// public class IncidentTests
// {
//     private readonly Random random = new();
//     private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;
//
//     private static readonly Func<Incident, object, Incident> evolve =
//         (incident, @event) =>
//         {
//             return @event switch
//             {
//                 IncidentLogged logged => Incident.Create(logged),
//                 IncidentCategorised categorised => incident.Apply(categorised),
//                 IncidentPrioritised prioritised => incident.Apply(prioritised),
//                 AgentRespondedToIncident agentResponded => incident.Apply(agentResponded),
//                 CustomerRespondedToIncident customerResponded => incident.Apply(customerResponded),
//                 IncidentResolved resolved => incident.Apply(resolved),
//                 ResolutionAcknowledgedByCustomer acknowledged => incident.Apply(acknowledged),
//                 IncidentClosed closed => incident.Apply(closed),
//                 _ => incident
//             };
//         };
//
//     private readonly HandlerSpecification<Incident> Spec = Specification.For(evolve);
//
//     private class DummyProductPriceCalculator: IProductPriceCalculator
//     {
//         private readonly decimal price;
//
//         public DummyProductPriceCalculator(decimal price) => this.price = price;
//
//         public IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems) =>
//             productItems.Select(pi => PricedProductItem.For(pi, price)).ToList();
//     }
//
//     [Fact]
//     public void GivenNonExistingIncident_WhenOpenWithValidParams_ThenSucceeds()
//     {
//         var incidentId = Guid.NewGuid();
//         var customerId = Guid.NewGuid();
//         var contact = new Contact(ContactChannel.Email, EmailAddress: "john@doe.com");
//         var description = Guid.NewGuid().ToString();
//         var loggedBy = Guid.NewGuid();
//
//         Spec.Given()
//             .When(_ =>
//                 IncidentService.Handle(
//                 () => now,
//                 new LogIncident(incidentId, customerId, contact, description, loggedBy)
//             ))
//             .Then(new IncidentLogged(IncidentId, clientId));
//     }
//
//     [Fact]
//     public void GivenOpenIncident_WhenAddProductWithValidParams_ThenSucceeds()
//     {
//         var IncidentId = Guid.NewGuid();
//
//         var productItem = ValidProductItem();
//         var price = random.Next(1000);
//         var priceCalculator = new DummyProductPriceCalculator(price);
//
//         Spec.Given(IncidentOpened(IncidentId))
//             .When(cart => cart.AddProduct(priceCalculator, productItem))
//             .Then(new ProductAdded(IncidentId, PricedProductItem.For(productItem, price)));
//     }
// }
//
// public static class IncidentEventsBuilder
// {
//     public static IncidentOpened IncidentOpened(Guid IncidentId)
//     {
//         var clientId = Guid.NewGuid();
//
//         return new IncidentOpened(IncidentId, clientId);
//     }
// }
//
// public static class ProductItemBuilder
// {
//     private static readonly Random Random = new();
//
//     public static ProductItem ValidProductItem() =>
//         ProductItem.From(Guid.NewGuid(), Random.Next(100));
// }
//
// public static class AggregateTestExtensions<TAggregate> where TAggregate : Aggregate
// {
//     public static object[] Handle(Action<TAggregate> handle, TAggregate bankAccount)
//     {
//         handle(bankAccount);
//         return bankAccount.DequeueUncommittedEvents();
//     }
// }
