using System;
using NUnit.Framework;
using NSubstitute;
using AccessibleArena.Core.Interfaces;
using AccessibleArena.Core.Models;
using AccessibleArena.Core.Services;

namespace AccessibleArena.Tests
{
    [TestFixture]
    public class AnnouncementServiceTests
    {
        private IScreenReaderOutput _output;
        private AnnouncementService _service;

        private void MakeService(bool verbose = true)
        {
            _output = Substitute.For<IScreenReaderOutput>();
            _service = new AnnouncementService(_output, () => verbose);
        }

        [SetUp]
        public void SetUp() => MakeService();

        // --- Announce() basics ---

        [Test]
        public void Announce_Normal_CallsSpeakWithInterruptFalse()
        {
            _service.Announce("Hello", AnnouncementPriority.Normal);
            _output.Received(1).Speak("Hello", false);
        }

        [Test]
        public void Announce_Immediate_CallsSpeakWithInterruptTrue()
        {
            _service.Announce("Alert", AnnouncementPriority.Immediate);
            _output.Received(1).Speak("Alert", true);
        }

        [Test]
        public void Announce_NullMessage_DoesNotCallSpeak()
        {
            _service.Announce(null);
            _output.DidNotReceive().Speak(Arg.Any<string>(), Arg.Any<bool>());
        }

        [Test]
        public void Announce_EmptyMessage_DoesNotCallSpeak()
        {
            _service.Announce("");
            _output.DidNotReceive().Speak(Arg.Any<string>(), Arg.Any<bool>());
        }

        // --- Deduplication ---

        [Test]
        public void Announce_SameMessageTwiceAtNormal_SpeakCalledOnce()
        {
            _service.Announce("Same", AnnouncementPriority.Normal);
            _service.Announce("Same", AnnouncementPriority.Normal);
            _output.Received(1).Speak("Same", false);
        }

        [Test]
        public void Announce_SameMessageAtHigh_BypassesDedup()
        {
            _service.Announce("Same", AnnouncementPriority.Normal);
            _service.Announce("Same", AnnouncementPriority.High);
            _output.Received(2).Speak("Same", Arg.Any<bool>());
        }

        [Test]
        public void Announce_DifferentMessages_BothSpoken()
        {
            _service.Announce("First");
            _service.Announce("Second");
            _output.Received(1).Speak("First", false);
            _output.Received(1).Speak("Second", false);
        }

        // --- SetEnabled ---

        [Test]
        public void SetEnabled_False_SubsequentAnnounceDoesNotSpeak()
        {
            _service.SetEnabled(false);
            _service.Announce("Should not speak");
            _output.DidNotReceive().Speak(Arg.Any<string>(), Arg.Any<bool>());
        }

        [Test]
        public void IsEnabled_ReflectsSetEnabled()
        {
            Assert.IsTrue(_service.IsEnabled);
            _service.SetEnabled(false);
            Assert.IsFalse(_service.IsEnabled);
        }

        // --- Verbose ---

        [Test]
        public void AnnounceVerbose_VerboseTrue_CallsSpeak()
        {
            MakeService(verbose: true);
            _service.AnnounceVerbose("Verbose on");
            _output.Received(1).Speak("Verbose on", false);
        }

        [Test]
        public void AnnounceVerbose_VerboseFalse_DoesNotSpeak()
        {
            MakeService(verbose: false);
            _service.AnnounceVerbose("Verbose off");
            _output.DidNotReceive().Speak(Arg.Any<string>(), Arg.Any<bool>());
        }

        // --- Silence ---

        [Test]
        public void Silence_CallsOutputSilence()
        {
            _service.Silence();
            _output.Received(1).Silence();
        }

        // --- RepeatLastAnnouncement ---

        [Test]
        public void RepeatLastAnnouncement_SpeaksLastMessageWithInterrupt()
        {
            _service.Announce("The message");
            _output.ClearReceivedCalls();

            _service.RepeatLastAnnouncement();
            _output.Received(1).Speak("The message", true);
        }

        [Test]
        public void RepeatLastAnnouncement_NoLastMessage_DoesNotSpeak()
        {
            _service.RepeatLastAnnouncement();
            _output.DidNotReceive().Speak(Arg.Any<string>(), Arg.Any<bool>());
        }

        // --- History ---

        [Test]
        public void LogToHistory_AddsMessage()
        {
            _service.LogToHistory("event A");
            Assert.AreEqual(1, _service.History.Count);
            Assert.AreEqual("event A", _service.History[0]);
        }

        [Test]
        public void LogToHistory_ConsecutiveDuplicate_NotAdded()
        {
            _service.LogToHistory("event A");
            _service.LogToHistory("event A");
            Assert.AreEqual(1, _service.History.Count);
        }

        [Test]
        public void LogToHistory_NonConsecutiveDuplicate_IsAdded()
        {
            _service.LogToHistory("A");
            _service.LogToHistory("B");
            _service.LogToHistory("A");
            Assert.AreEqual(3, _service.History.Count);
        }

        [Test]
        public void LogToHistory_NullOrEmpty_NotAdded()
        {
            _service.LogToHistory(null);
            _service.LogToHistory("");
            Assert.AreEqual(0, _service.History.Count);
        }

        [Test]
        public void ClearHistory_EmptiesHistory()
        {
            _service.LogToHistory("A");
            _service.LogToHistory("B");
            _service.ClearHistory();
            Assert.AreEqual(0, _service.History.Count);
        }
    }
}
