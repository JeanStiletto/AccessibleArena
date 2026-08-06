using System;
using System.Text;
using NUnit.Framework;
using AccessibleArena.Core.Speech;

namespace AccessibleArena.Tests
{
    /// <summary>
    /// Text marshalling for the Prism speech ABI. Prism strict-validates UTF-8 and drops the
    /// whole utterance on the first invalid byte, so a regression here is silent: the mod keeps
    /// running and simply stops speaking card names that carry an accent. These tests pin the
    /// encoding down without needing prism.dll present.
    /// </summary>
    [TestFixture]
    public class PrismInteropTests
    {
        [Test]
        public void ToUtf8_AppendsNulTerminator()
        {
            byte[] encoded = PrismInterop.ToUtf8("Ok");

            Assert.That(encoded, Is.EqualTo(new byte[] { (byte)'O', (byte)'k', 0 }));
        }

        [Test]
        public void ToUtf8_EncodesNonAsciiAsUtf8NotAnsi()
        {
            byte[] encoded = PrismInterop.ToUtf8("Lärm");

            // "ä" is two bytes in UTF-8 (0xC3 0xA4) and one byte in any ANSI codepage. The
            // length check is what catches an accidental fall-back to ANSI marshalling.
            Assert.That(encoded.Length, Is.EqualTo(6), "expected 5 UTF-8 bytes plus the terminator");
            Assert.That(encoded[1], Is.EqualTo(0xC3));
            Assert.That(encoded[2], Is.EqualTo(0xA4));
            Assert.That(encoded[encoded.Length - 1], Is.EqualTo(0));
        }

        [Test]
        public void ToUtf8_EncodesTextOutsideTheBasicMultilingualPlane()
        {
            // Surrogate pairs have to survive as a single four-byte sequence; splitting them
            // produces invalid UTF-8 and Prism rejects the utterance.
            byte[] encoded = PrismInterop.ToUtf8("\U0001F600");

            Assert.That(encoded, Is.EqualTo(new byte[] { 0xF0, 0x9F, 0x98, 0x80, 0 }));
        }

        [Test]
        public void ToUtf8_ReturnsNullForNothingToSay()
        {
            Assert.That(PrismInterop.ToUtf8(null), Is.Null);
            Assert.That(PrismInterop.ToUtf8(string.Empty), Is.Null);
        }

        [Test]
        public void FromUtf8_ReadsNulTerminatedTextBack()
        {
            byte[] source = Encoding.UTF8.GetBytes("NVDA\0");
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(source,
                System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                Assert.That(PrismInterop.FromUtf8(handle.AddrOfPinnedObject()), Is.EqualTo("NVDA"));
            }
            finally
            {
                handle.Free();
            }
        }

        [Test]
        public void FromUtf8_ReturnsNullForNullPointer()
        {
            Assert.That(PrismInterop.FromUtf8(IntPtr.Zero), Is.Null);
        }
    }
}
