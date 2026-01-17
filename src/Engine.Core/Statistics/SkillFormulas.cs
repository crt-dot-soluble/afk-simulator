namespace Engine.Core.Statistics;

internal static class SkillFormulas
{
    public static int CalculateLevel(double experience)
    {
        if (experience <= 0)
        {
            return 1;
        }

        var scaled = Math.Sqrt(experience / 100d) * 10d;
        return Math.Clamp((int)Math.Floor(scaled), 1, 120);
    }
}
