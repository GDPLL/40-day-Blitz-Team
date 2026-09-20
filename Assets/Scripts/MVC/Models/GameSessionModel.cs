using System;

[Serializable]
public sealed class GameSessionModel
{
    public enum Phase { Lobby, Playing, Results }
    public Phase CurrentPhase { get; private set; } = Phase.Lobby;
    public int PlayerCount { get; private set; }
    public void SetPhase(Phase phase) => CurrentPhase = phase;
    public void SetPlayerCount(int count) => PlayerCount = Math.Max(0, count);
}
