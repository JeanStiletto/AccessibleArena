using System;
using System.Text;

namespace AccessibleArena.Core.Speech
{
    /// <summary>
    /// Pure helpers for SSIP, the line protocol of speech-dispatcher (the speech server Orca
    /// and most Linux desktops use). No I/O lives here, so the framing rules are unit-tested
    /// without a server.
    ///
    /// Protocol essentials:
    /// <list type="bullet">
    /// <item>Commands and replies are CRLF-terminated UTF-8 lines.</item>
    /// <item>A reply is one or more lines <c>NNN-text</c> ending in a final <c>NNN text</c>;
    /// 2xx is success, anything else an error.</item>
    /// <item><c>SPEAK</c> answers 230, then takes the message body line by line, terminated by
    /// a line holding a single dot. Body lines starting with a dot get a second dot.</item>
    /// </list>
    /// </summary>
    internal static class SsipProtocol
    {
        internal const string Eol = "\r\n";

        /// <summary>Default TCP port speech-dispatcher uses for its inet socket.</summary>
        internal const int DefaultPort = 6560;

        /// <summary>
        /// Builds the SPEAK body for <paramref name="text"/>: line endings normalised to CRLF,
        /// dot-stuffed, and closed with the end-of-data marker. Returns null for nothing to say.
        /// </summary>
        internal static string BuildSpeakBody(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var sb = new StringBuilder(text.Length + 16);
            foreach (string line in lines)
            {
                if (line.StartsWith(".", StringComparison.Ordinal))
                    sb.Append('.');
                sb.Append(line).Append(Eol);
            }
            sb.Append('.').Append(Eol);
            return sb.ToString();
        }

        /// <summary>
        /// Parses one reply line. Returns false when the line is not a reply line at all.
        /// <paramref name="isFinal"/> is true for the last line of a reply (space after the code).
        /// </summary>
        internal static bool TryParseReplyLine(string line, out int code, out bool isFinal, out string text)
        {
            code = 0;
            isFinal = false;
            text = null;

            if (line == null || line.Length < 3)
                return false;

            for (int i = 0; i < 3; i++)
            {
                char c = line[i];
                if (c < '0' || c > '9')
                    return false;
                code = code * 10 + (c - '0');
            }

            if (line.Length == 3)
            {
                isFinal = true;
                text = string.Empty;
                return true;
            }

            char sep = line[3];
            if (sep != ' ' && sep != '-')
                return false;

            isFinal = sep == ' ';
            text = line.Substring(4);
            return true;
        }

        internal static bool IsSuccess(int code) => code >= 200 && code < 300;

        /// <summary>SSIP rates run from -100 (slowest) to 100 (fastest); 0 is the server default.</summary>
        internal static int ClampRate(int rate) => rate < -100 ? -100 : rate > 100 ? 100 : rate;

        /// <summary>
        /// Client name in SSIP's <c>user:application:component</c> form. Speech-dispatcher
        /// shows it in its own logs and in tools like <c>spd-conf</c>.
        /// </summary>
        internal static string ClientNameCommand(string user)
        {
            string safeUser = Sanitize(string.IsNullOrEmpty(user) ? "user" : user);
            return $"SET SELF CLIENT_NAME {safeUser}:accessible_arena:main";
        }

        /// <summary>
        /// Parses a <c>SPEECHD_ADDRESS</c> value for the inet form,
        /// <c>inet_socket[:host[:port]]</c>. Any other form (unix_socket) is reported as false:
        /// the mod cannot open Unix sockets from inside Wine.
        /// </summary>
        internal static bool TryParseInetAddress(string value, out string host, out int port)
        {
            host = "127.0.0.1";
            port = DefaultPort;

            if (string.IsNullOrEmpty(value))
                return false;

            string[] parts = value.Trim().Split(':');
            if (!string.Equals(parts[0], "inet_socket", StringComparison.OrdinalIgnoreCase))
                return false;

            if (parts.Length > 1 && parts[1].Length > 0)
                host = parts[1];

            if (parts.Length > 2)
            {
                int parsed;
                if (!int.TryParse(parts[2], out parsed) || parsed <= 0 || parsed > 65535)
                    return false;
                port = parsed;
            }
            return true;
        }

        /// <summary>
        /// Quotes one argument for a Windows command line so that it comes back unchanged
        /// through CommandLineToArgvW rules — which is also what Wine uses to build the argv of a
        /// native Linux program started from Windows code.
        /// </summary>
        internal static string QuoteArgument(string arg)
        {
            if (arg == null)
                arg = string.Empty;

            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
                return arg;

            var sb = new StringBuilder(arg.Length + 2);
            sb.Append('"');
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    sb.Append('\\', backslashes);
                    sb.Append(c);
                }
                backslashes = 0;
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        private static string Sanitize(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }
    }
}
