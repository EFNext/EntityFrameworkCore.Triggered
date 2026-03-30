using System;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered;

namespace BlazorTests.Triggers.Counts
{
    public class SetCreatedOn : IBeforeSaveAsyncTrigger<Count>
    {
        public Task BeforeSaveAsync(ITriggerContext<Count> context, CancellationToken cancellationToken)
        {
            context.Entity.CreatedOn = DateTime.UtcNow;

            return Task.CompletedTask;
        }
    }
}
