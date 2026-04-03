using System;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered;
using StudentManager.Traits;

namespace StudentManager.Triggers.Traits.SoftDelete
{
    class EnsureSoftDelete : IBeforeSaveAsyncTrigger<ISoftDelete>
    {
        readonly ApplicationDbContext _applicationContext;

        public EnsureSoftDelete(ApplicationDbContext applicationContext)
        {
            _applicationContext = applicationContext;
        }

        public Task BeforeSaveAsync(ITriggerContext<ISoftDelete> context, CancellationToken cancellationToken)
        {
            if (context.ChangeType == ChangeType.Deleted)
            {
                context.Entity.DeletedOn = DateTimeOffset.Now;
                _applicationContext.Entry(context.Entity).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }

            return Task.CompletedTask;
        }
    }
}
