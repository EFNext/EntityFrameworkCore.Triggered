namespace EntityFrameworkCore.Triggered.Benchmarks.Triggers
{
    // These "noise" triggers implement IBeforeSaveTrigger<StudentCourse> (not IBeforeSaveTrigger<Student>).
    // They are used to simulate an assembly containing several triggers where only a subset applies
    // to the entity being saved — a typical real-world scenario.
    // AddAssemblyTriggers will discover and register them, but they will never be resolved
    // during a SaveChanges that only tracks Student entities.

    public class DummyStudentCourseTrigger1 : IBeforeSaveTrigger<StudentCourse>
    {
        public void BeforeSave(ITriggerContext<StudentCourse> context) { }
    }

    public class DummyStudentCourseTrigger2 : IBeforeSaveTrigger<StudentCourse>
    {
        public void BeforeSave(ITriggerContext<StudentCourse> context) { }
    }

    public class DummyStudentCourseTrigger3 : IBeforeSaveTrigger<StudentCourse>
    {
        public void BeforeSave(ITriggerContext<StudentCourse> context) { }
    }

    public class DummyStudentCourseTrigger4 : IBeforeSaveTrigger<StudentCourse>
    {
        public void BeforeSave(ITriggerContext<StudentCourse> context) { }
    }

    public class DummyStudentCourseTrigger5 : IBeforeSaveTrigger<StudentCourse>
    {
        public void BeforeSave(ITriggerContext<StudentCourse> context) { }
    }
}
