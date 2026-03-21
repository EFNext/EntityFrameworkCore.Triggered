using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered;

namespace PrimarySchool.Triggers
{
    public class StudentSignupToMandatoryCourses : IBeforeSaveAsyncTrigger<Student>
    {
        readonly ApplicationDbContext _applicationContext;

        public StudentSignupToMandatoryCourses(ApplicationDbContext applicationContext)
        {
            _applicationContext = applicationContext;
        }

        public Task BeforeSaveAsync(ITriggerContext<Student> context, CancellationToken cancellationToken)
        {
            var mandatoryCourses = _applicationContext.Courses
                .Where(x => x.IsMandatory)
                .ToList();

            foreach (var mandatoryCourse in mandatoryCourses)
            {
                var studentRegistration = _applicationContext.StudentCourses.Find(context.Entity.Id, mandatoryCourse.Id);
                if (studentRegistration == null)
                {
                    _applicationContext.StudentCourses.Add(new StudentCourse { StudentId = context.Entity.Id, CourseId = mandatoryCourse.Id });
                }
            }

            return Task.CompletedTask;
        }
    }
}
