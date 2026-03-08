namespace DEMATSYNTH.Enums
{
    [Flags]
    internal enum DMSState
    {
        Idle = 0,
        WaitingForRetrieveDialog = 1,
        WaitingForRetrieveCompletion = 2,
        WaitingForDesynthDialog = 4,
        WaitingForDesynthResult = 8,
    }
}
