using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered;
using Microsoft.EntityFrameworkCore;

namespace StudentManager.Triggers.Courses
{
    public class AutoSignupStudents : IBeforeSaveAsyncTrigger<Course>
    {
        readonly ApplicationDbContext _applicationContext;

        public AutoSignupStudents(ApplicationDbContext applicationContext)
        {
            _applicationContext = applicationContext;
        }

        public async Task BeforeSaveAsync(ITriggerContext<Course> context, CancellationToken cancellationToken)
        {
            if (context.Entity.IsMandatory)
            {
                // Fetch all students that are not yet signup up for this course
                var students = await _applicationContext.Students
                    .Where(x => x.Courses.All(course => course.CourseId != context.Entity.Id))
                    .ToListAsync(cancellationToken);

                foreach (var student in students)
                {
                    _applicationContext.StudentCourses.Add(new StudentCourse { Student = student, Course = context.Entity });
                }
            }
        }
    }
}
