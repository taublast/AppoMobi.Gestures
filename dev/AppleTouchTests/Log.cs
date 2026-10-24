namespace AppleTouchTests;

/// The only output channel. simctl's console carries Console.WriteLine; Debug.WriteLine does not
/// reach it, so every line the harness parses goes through here.
public static class Log
{
    public const string Tag = "TOUCHTEST";

    public static void Line(string text)
    {
        Console.WriteLine($"{Tag} {text}");
        Console.Out.Flush();
    }

    public static void Result(string name, bool pass, string detail)
        => Line($"{name} {(pass ? "PASS" : "FAIL")} {detail}");
}
