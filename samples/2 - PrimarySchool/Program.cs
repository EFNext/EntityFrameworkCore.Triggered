using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrimarySchool;
using PrimarySchool.Triggers;

var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options => {
        options
            .UseInMemoryDatabase("PrimarySchool")
            .UseTriggers(triggerOptions => {
                triggerOptions.AddTrigger<StudentSignupToMandatoryCourses>();
            });
    })
    .BuildServiceProvider();

var serviceScope = serviceProvider.CreateScope();
var applicationContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

applicationContext.Courses.Add(new Course {
    Id = 1,
    DisplayName = "Mathematics",
    IsMandatory = true
});

applicationContext.Courses.Add(new Course {
    Id = 2,
    DisplayName = "Art",
    IsMandatory = false
});

applicationContext.SaveChanges();

applicationContext.Students.Add(new Student {
    Id = 1,
    DisplayName = "Joe"
});

applicationContext.SaveChanges();

var joesCourses = applicationContext.StudentCourses
    .Where(x => x.StudentId == 1)
    .Select(x => x.Course.DisplayName);

foreach (var course in joesCourses)
{
    Console.WriteLine($"Joe is registered for course {course}");
}
