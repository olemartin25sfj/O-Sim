namespace AlarmService;

public static class AlarmEvaluator
{
    public static bool ShouldTrigger(double speed, double threshold) => speed > threshold;
}
