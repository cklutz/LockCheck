namespace LockCheck
{
    public enum ApplicationType
    {
        // Members must have the same values as in NativeMethods.RM_APP_TYPE

        Unknown = 0,
        MainWindow = 1,
        OtherWindow = 2,
        Service = 3,
        Explorer = 4,
        Console = 5,
        Critical = 1000
    }
}