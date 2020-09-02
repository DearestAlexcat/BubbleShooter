using UnityEngine;

[CreateAssetMenu(fileName = "GameEvents", menuName = "BubbleShooter/new GameEvents")]
public class GameEvents : ScriptableObject
{
    public delegate void ScoreUpdatedCallback(float scores);
    public ScoreUpdatedCallback ScoreUpdated = null;

    public delegate void RefreshUIElementsCallback();
    public RefreshUIElementsCallback RefreshUIElements = null;

    public delegate void SetHighSoresCallback();
    public SetHighSoresCallback SetHighSores = null;

    [HideInInspector]
    public BallController ballController;

    [HideInInspector]
    public float score;
    [HideInInspector]
    public int round;
}
