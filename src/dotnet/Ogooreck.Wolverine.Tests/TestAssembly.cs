using Ogooreck.Wolverine.Tests.Fixtures;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AssemblyFixture(typeof(AppApi))]
[assembly: AssemblyFixture(typeof(BareApp))]
[assembly: Parallelization(Mode = ParallelMode.None)]
