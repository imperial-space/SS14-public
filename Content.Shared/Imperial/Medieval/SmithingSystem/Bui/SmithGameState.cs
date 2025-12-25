namespace Content.Shared.Imperial.Medieval.SmithingSystem.Bui;

public sealed class SmithGameState(int steps, float maxGameTime)
{
    private readonly Dictionary<SmithHitState, int> _hitStates = new()
    {
        { SmithHitState.Good, 0 },
        { SmithHitState.Neutral, 0 },
        { SmithHitState.Missed, 0 },
    };

    private int _misclickPenalty;
    public TimeSpan ForceEndTime;
    public TimeSpan StartTime;

    public bool Started { get; private set; }
    public int StepsTotal { get; } = steps;

    public int CompletedSteps { get; private set; }

    public void Start(TimeSpan currentTime)
    {
        StartTime = currentTime;
        ForceEndTime = currentTime + TimeSpan.FromSeconds(maxGameTime + 5f);

        Started = true;
    }

    public void AddStep(SmithHitState state, bool incrementSteps = true)
    {
        if (CompletedSteps >= StepsTotal)
            return;

        if (incrementSteps)
        {
            _hitStates[state]++;
            CompletedSteps++;
            return;
        }

        if (state == SmithHitState.Missed)
            _misclickPenalty++;
    }

    public int CalculateScore()
    {
        var score = 0;

        score += _hitStates[SmithHitState.Good];
        score -= _hitStates[SmithHitState.Missed];

        var forceEnded = StepsTotal - CompletedSteps;
        score -= forceEnded;

        score -= _misclickPenalty;

        return score;
    }
}
