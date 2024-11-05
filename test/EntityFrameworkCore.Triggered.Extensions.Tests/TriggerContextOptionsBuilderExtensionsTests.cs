using EntityFrameworkCore.Triggered.Infrastructure;
using EntityFrameworkCore.Triggered.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityFrameworkCore.Triggered.Extensions.Tests;

public class TriggerContextOptionsBuilderExtensionsTests
{
    [Fact]
    public void AddAssemblyTriggers_AbstractTrigger_GetsIgnored()
    {
        var context = new DbContextOptionsBuilder();
        var builder = new TriggersContextOptionsBuilder(context);

        builder.AddAssemblyTriggers();

        var triggerOptionExtension = context.Options.Extensions.OfType<TriggersOptionExtension>().Single();

        // Ensure that we did not register the AbstractTrigger
        Assert.DoesNotContain(triggerOptionExtension.Triggers, x => ReferenceEquals(x.typeOrInstance, typeof(AbstractTrigger)));
    }
}
