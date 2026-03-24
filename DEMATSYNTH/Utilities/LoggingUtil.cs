using System.Diagnostics;

namespace DEMATSYNTH.Utilities;

internal static class LoggingUtil
{

    private static string GetCallerPrefix()
    {
        var stackFrame = new StackFrame(3);
        var method = stackFrame.GetMethod();
        var className = method?.DeclaringType?.Name;
        var methodName = method?.Name;

        if (className != null && methodName != null)
        {
            return $"[{className}.{methodName}]";
        }
        else if (className != null)
        {
            return $"[{className}]";
        }
        else if (methodName != null)
        {
            return $"[{methodName}]";
        }
        return string.Empty;
    }

    private static string FormatMessage(string message, string prefix = null)
    {
        var callerPrefix = prefix ?? GetCallerPrefix();
        return $"{callerPrefix} {message}";
    }

    public static void Verbose(string message, string prefix = null, bool debugOnly = false)
    {
        var formattedMessage = FormatMessage(message, prefix);
        PluginLog.Verbose(formattedMessage);
    }

    public static void Debug(string message, string prefix = null, bool debugOnly = false)
    {
        if (debugOnly)
        {
#if DEBUG
            var formattedMessage = FormatMessage(message, prefix);
            PluginLog.Debug(formattedMessage);
#endif
        }
        else
        {
            var formattedMessage = FormatMessage(message, prefix);
            PluginLog.Debug(formattedMessage);
        }
    }

    public static void Info(string message, string prefix = null, bool debugOnly = false)
    {
        if (debugOnly)
        {
#if DEBUG
            var formattedMessage = FormatMessage(message, prefix);
            PluginLog.Information(formattedMessage);
#endif
        }
        else
        {
            var formattedMessage = FormatMessage(message, prefix);
            PluginLog.Information(formattedMessage);
        }
    }

    public static void ChatInfo(string s, string prefix = null)
    {
        if (prefix == null)
        {
            Svc.Chat.Print(s);
            PluginLog.Information(s);
        }
        else
        {
            Svc.Chat.Print($"{prefix} {s}");
            PluginLog.Information($"{prefix} {s}");
        }
    }

    public static void Warning(string message, string prefix = null)
    {
        var formattedMessage = FormatMessage(message, prefix);
        PluginLog.Warning(formattedMessage);
    }

    public static void Error(string message, string prefix = null)
    {
        var formattedMessage = FormatMessage(message, prefix);
        PluginLog.Error(formattedMessage);
    }

    public static void Fatal(string message, string prefix = null)
    {
        var formattedMessage = FormatMessage(message, prefix);
        PluginLog.Fatal(formattedMessage);
    }
}
