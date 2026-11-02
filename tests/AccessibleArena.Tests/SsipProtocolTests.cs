using NUnit.Framework;
using AccessibleArena.Core.Speech;

namespace AccessibleArena.Tests
{
    /// <summary>
    /// Framing rules for SSIP, the speech-dispatcher protocol the Linux (Wine/Proton) speech
    /// channel uses. A framing mistake is silent in the game: speech-dispatcher keeps waiting
    /// for the end of the message, and every later announcement is lost with it.
    /// </summary>
    [TestFixture]
    public class SsipProtocolTests
    {
        [Test]
        public void BuildSpeakBody_EndsWithDotLine()
        {
            Assert.That(SsipProtocol.BuildSpeakBody("Hello"), Is.EqualTo("Hello\r\n.\r\n"));
        }

        [Test]
        public void BuildSpeakBody_DoublesLeadingDots()
        {
            // A lone "." would end the message early; ".5 mana" would lose its dot.
            Assert.That(SsipProtocol.BuildSpeakBody(".\n.5 mana"), Is.EqualTo("..\r\n..5 mana\r\n.\r\n"));
        }

        [Test]
        public void BuildSpeakBody_NormalisesLineEndings()
        {
            Assert.That(SsipProtocol.BuildSpeakBody("a\nb\r\nc\rd"), Is.EqualTo("a\r\nb\r\nc\r\nd\r\n.\r\n"));
        }

        [Test]
        public void BuildSpeakBody_ReturnsNullForNothingToSay()
        {
            Assert.That(SsipProtocol.BuildSpeakBody(null), Is.Null);
            Assert.That(SsipProtocol.BuildSpeakBody(string.Empty), Is.Null);
        }

        [Test]
        public void TryParseReplyLine_ReadsContinuationAndFinalLines()
        {
            int code;
            bool isFinal;
            string text;

            Assert.That(SsipProtocol.TryParseReplyLine("225-17", out code, out isFinal, out text), Is.True);
            Assert.That(code, Is.EqualTo(225));
            Assert.That(isFinal, Is.False);
            Assert.That(text, Is.EqualTo("17"));

            Assert.That(SsipProtocol.TryParseReplyLine("225 OK MESSAGE QUEUED", out code, out isFinal, out text), Is.True);
            Assert.That(code, Is.EqualTo(225));
            Assert.That(isFinal, Is.True);
            Assert.That(text, Is.EqualTo("OK MESSAGE QUEUED"));
        }

        [Test]
        public void TryParseReplyLine_RejectsNonReplies()
        {
            int code;
            bool isFinal;
            string text;

            Assert.That(SsipProtocol.TryParseReplyLine(null, out code, out isFinal, out text), Is.False);
            Assert.That(SsipProtocol.TryParseReplyLine("OK", out code, out isFinal, out text), Is.False);
            Assert.That(SsipProtocol.TryParseReplyLine("2x5 OK", out code, out isFinal, out text), Is.False);
            Assert.That(SsipProtocol.TryParseReplyLine("225:OK", out code, out isFinal, out text), Is.False);
        }

        [Test]
        public void IsSuccess_OnlyFor2xx()
        {
            Assert.That(SsipProtocol.IsSuccess(208), Is.True);
            Assert.That(SsipProtocol.IsSuccess(230), Is.True);
            Assert.That(SsipProtocol.IsSuccess(300), Is.False);
            Assert.That(SsipProtocol.IsSuccess(410), Is.False);
        }

        [Test]
        public void ClientNameCommand_SanitisesTheUserName()
        {
            Assert.That(SsipProtocol.ClientNameCommand("jan kowalski:x"),
                        Is.EqualTo("SET SELF CLIENT_NAME jan_kowalski_x:accessible_arena:main"));
            Assert.That(SsipProtocol.ClientNameCommand(null),
                        Is.EqualTo("SET SELF CLIENT_NAME user:accessible_arena:main"));
        }

        [Test]
        public void TryParseInetAddress_ReadsHostAndPort()
        {
            string host;
            int port;

            Assert.That(SsipProtocol.TryParseInetAddress("inet_socket:192.168.1.5:7000", out host, out port), Is.True);
            Assert.That(host, Is.EqualTo("192.168.1.5"));
            Assert.That(port, Is.EqualTo(7000));

            Assert.That(SsipProtocol.TryParseInetAddress("inet_socket", out host, out port), Is.True);
            Assert.That(host, Is.EqualTo("127.0.0.1"));
            Assert.That(port, Is.EqualTo(SsipProtocol.DefaultPort));
        }

        [Test]
        public void TryParseInetAddress_RejectsUnixAndBrokenValues()
        {
            string host;
            int port;

            Assert.That(SsipProtocol.TryParseInetAddress("unix_socket:/run/user/1000/speechd.sock", out host, out port), Is.False);
            Assert.That(SsipProtocol.TryParseInetAddress("inet_socket:localhost:99999", out host, out port), Is.False);
            Assert.That(SsipProtocol.TryParseInetAddress(null, out host, out port), Is.False);
        }

        [TestCase("plain", "plain")]
        [TestCase("", "\"\"")]
        [TestCase("two words", "\"two words\"")]
        [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [TestCase("C:\\dir\\", "C:\\dir\\")]
        [TestCase("C:\\my dir\\", "\"C:\\my dir\\\\\"")]
        public void QuoteArgument_FollowsCommandLineToArgvRules(string input, string expected)
        {
            Assert.That(SsipProtocol.QuoteArgument(input), Is.EqualTo(expected));
        }
    }
}
