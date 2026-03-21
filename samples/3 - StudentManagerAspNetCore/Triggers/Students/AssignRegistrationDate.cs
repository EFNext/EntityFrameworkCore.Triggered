using System;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered;

namespace StudentManager.Triggers.Students
{
    public class AssignRegistrationDate : IBeforeSaveAsyncTrigger<Student>
    {
        public Task BeforeSaveAsync(ITriggerContext<Student> context, CancellationToken cancellationToken)
        {
            context.Entity.RegistrationDate = DateTime.Today;

            return Task.CompletedTask;
        }
    }
}
