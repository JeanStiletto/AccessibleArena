using System.Text;
using NUnit.Framework;
using AccessibleArena.Core.Speech;

namespace AccessibleArena.Tests
{
    /// <summary>
    /// Wire format for the nvda2speechd server. The expected bytes were produced by rmp-serde 1.3
    /// (the version nvda2speechd builds with) from its own Request enum; the server silently
    /// ignores anything it cannot decode, so a mismatch would mean a mute mod.
    /// </summary>
    [TestFixture]
    public class Nvda2SpeechdProtocolTests
    {
        [Test]
        public void EncodeSpeak_MatchesRmpSerde()
        {
            byte[] expected =
            {
                0x81, 0xA9, 0x53, 0x70, 0x65, 0x61, 0x6B, 0x54, 0x65, 0x78, 0x74, 0xA2, 0x48, 0x69
            };
            Assert.That(Nvda2SpeechdProtocol.EncodeSpeak("Hi"), Is.EqualTo(expected));
        }

        [Test]
        public void EncodeCancel_MatchesRmpSerde()
        {
            byte[] expected =
            {
                0xAC, 0x43, 0x61, 0x6E, 0x63, 0x65, 0x6C, 0x53, 0x70, 0x65, 0x65, 0x63, 0x68
            };
            Assert.That(Nvda2SpeechdProtocol.EncodeCancel(), Is.EqualTo(expected));
        }

        [TestCase(31, 0xBF, 1)]
        [TestCase(32, 0xD9, 2)]
        [TestCase(300, 0xDA, 3)]
        [TestCase(70000, 0xDB, 5)]
        public void MsgPackString_PicksTheSmallestHeader(int length, int firstByte, int headerLength)
        {
            byte[] encoded = Nvda2SpeechdProtocol.MsgPackString(new string('a', length));
            Assert.That(encoded[0], Is.EqualTo(firstByte));
            Assert.That(encoded.Length, Is.EqualTo(headerLength + length));
        }

        [Test]
        public void MsgPackString_CountsUtf8BytesNotChars()
        {
            byte[] encoded = Nvda2SpeechdProtocol.MsgPackString("ż");
            Assert.That(encoded, Is.EqualTo(new byte[] { 0xA2, 0xC5, 0xBC }));
        }

        [Test]
        public void BuildClientFrame_MasksPayload()
        {
            byte[] mask = { 1, 2, 3, 4 };
            byte[] frame = Nvda2SpeechdProtocol.BuildClientFrame(0x2, Encoding.ASCII.GetBytes("abcde"), mask);

            Assert.That(frame[0], Is.EqualTo(0x82), "FIN + binary opcode");
            Assert.That(frame[1], Is.EqualTo(0x80 | 5), "mask bit + length");
            Assert.That(new[] { frame[2], frame[3], frame[4], frame[5] }, Is.EqualTo(mask));
            Assert.That(frame[6], Is.EqualTo((byte)('a' ^ 1)));
            Assert.That(frame[10], Is.EqualTo((byte)('e' ^ 1)));
        }

        [Test]
        public void BuildClientFrame_UsesExtendedLengths()
        {
            byte[] mask = { 0, 0, 0, 0 };

            byte[] medium = Nvda2SpeechdProtocol.BuildClientFrame(0x2, new byte[300], mask);
            Assert.That(medium[1], Is.EqualTo(0x80 | 126));
            Assert.That(medium[2] * 256 + medium[3], Is.EqualTo(300));
            Assert.That(medium.Length, Is.EqualTo(2 + 2 + 4 + 300));

            byte[] large = Nvda2SpeechdProtocol.BuildClientFrame(0x2, new byte[70000], mask);
            Assert.That(large[1], Is.EqualTo(0x80 | 127));
            Assert.That(large.Length, Is.EqualTo(2 + 8 + 4 + 70000));
        }

        [Test]
        public void TryParseHost_ReadsNvda2SpeechdHostVariable()
        {
            string host, path;
            int port;

            Assert.That(Nvda2SpeechdProtocol.TryParseHost("ws://localhost:4000", out host, out port, out path), Is.True);
            Assert.That(host, Is.EqualTo("localhost"));
            Assert.That(port, Is.EqualTo(4000));
            Assert.That(path, Is.EqualTo("/"));

            Assert.That(Nvda2SpeechdProtocol.TryParseHost("ws://127.0.0.1", out host, out port, out path), Is.True);
            Assert.That(port, Is.EqualTo(Nvda2SpeechdProtocol.DefaultPort));

            Assert.That(Nvda2SpeechdProtocol.TryParseHost("wss://example.com", out host, out port, out path), Is.False);
            Assert.That(Nvda2SpeechdProtocol.TryParseHost(null, out host, out port, out path), Is.False);
        }
    }
}
