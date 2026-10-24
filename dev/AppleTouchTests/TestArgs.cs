namespace AppleTouchTests;

public static class TestArgs
{
    /// simctl hands launch arguments to the app through NSProcessInfo; they are not in
    /// Environment.GetCommandLineArgs(), which carries only the executable path.
    public static bool RunTests()
    {
        foreach (var a in All())
            if (string.Equals(a, "--run-tests", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    static IEnumerable<string> All()
    {
        foreach (var a in Environment.GetCommandLineArgs()) yield return a;
#if IOS || MACCATALYST
        foreach (var a in Foundation.NSProcessInfo.ProcessInfo.Arguments) yield return a;
#endif
    }
}
