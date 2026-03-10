namespace PinkRooster.Shared.Constants;

public static class ApiRoutes
{
    private const string Base = "api";

    public static class ActivityLogs
    {
        public const string Route = $"{Base}/activity-logs";
    }

    public static class Projects
    {
        public const string Route = $"{Base}/projects";
    }

    public static class Issues
    {
        public const string Route = $"{Base}/projects/{{projectId:long}}/issues";
    }

    public static class WorkPackages
    {
        public const string Route = $"{Base}/projects/{{projectId:long}}/work-packages";
    }
}
