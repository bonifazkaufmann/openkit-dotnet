﻿using NUnit.Framework;
using Dynatrace.OpenKit.Core.Configuration;
using Dynatrace.OpenKit.Providers;
using NSubstitute;
using Dynatrace.OpenKit.Protocol;

namespace Dynatrace.OpenKit.Core.Communication
{
    public class BeaconSendingContextTest
    {
        private AbstractConfiguration config;
        private IHTTPClientProvider clientProvider;
        private ITimingProvider timingProvider;
        private AbstractBeaconSendingState nonTerminalStateMock;

        [SetUp]
        public void Setup()
        {
            config = new TestConfiguration();
            clientProvider = Substitute.For<IHTTPClientProvider>();
            timingProvider = Substitute.For<ITimingProvider>();
            nonTerminalStateMock = Substitute.For<AbstractBeaconSendingState>(false);
        }
        
        [Test]
        public void ContextIsInitializedWithInitState()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            Assert.IsNotNull(target.CurrentState);
            Assert.AreEqual(typeof(BeaconSendingInitState), target.CurrentState.GetType());
        }

        [Test]
        public void CurrentStateIsSet()
        {
            BeaconSendingContext target = new BeaconSendingContext(config, clientProvider, timingProvider);

            target.CurrentState = nonTerminalStateMock;

            Assert.AreSame(nonTerminalStateMock, target.CurrentState);
        }

        [Test]
        public void ExecuteIsCalledOnCurrentState()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);
            target.CurrentState = nonTerminalStateMock;

            target.ExecuteCurrentState();

            nonTerminalStateMock.Received(1).Execute(target);
        }

        [Test]
        public void ResetEventIsOnInitSuccess()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            target.InitCompleted(true);
            var actual = target.WaitForInit();

            Assert.True(actual);
        }

        [Test]
        public void ResetEventIsOnInitFailed()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            target.InitCompleted(false);
            var actual = target.WaitForInit();

            Assert.False(actual);
        }

        [Test]
        public void IsShutdownRequestedIsSetCorrectly()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            Assert.False(target.IsShutdownRequested);

            target.RequestShutdown();

            Assert.True(target.IsShutdownRequested);            
        }

        [Test]
        public void LastOpenSessionSendTimeIsSet()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            Assert.AreEqual(0, target.LastOpenSessionBeaconSendTime);

            var expected = 17;
            target.LastOpenSessionBeaconSendTime = expected;

            Assert.AreEqual(expected, target.LastOpenSessionBeaconSendTime);
        }

        [Test]
        public void LastStatusCheckTimeIsSet()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            Assert.AreEqual(0, target.LastStatusCheckTime);

            var expected = 17;
            target.LastStatusCheckTime = expected;

            Assert.AreEqual(expected, target.LastStatusCheckTime);
        }

        [Test]
        public void LastTimeSyncTimeIsSet()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            Assert.AreEqual(0, target.LastTimeSyncTime);

            var expected = 17;
            target.LastTimeSyncTime = expected;

            Assert.AreEqual(expected, target.LastTimeSyncTime);
        }

        [Test]
        public void CanGetHttpClient()
        {
            var expected = Substitute.For<HTTPClient>("", "", 0, false);

            clientProvider.CreateClient(Arg.Any<HTTPClientConfiguration>()).Returns(expected);

            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            var actual = target.GetHTTPClient();

            Assert.NotNull(actual);
            Assert.AreSame(expected, actual);
            clientProvider.Received(1).CreateClient(Arg.Any<HTTPClientConfiguration>());
        }

        [Test]
        public void GetHttpClientUsesCurrentHttpConfig()
        {
            clientProvider
                .CreateClient(Arg.Any<HTTPClientConfiguration>())
                .Returns(Substitute.For<HTTPClient>("", "", 0, false));

            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            var actual = target.GetHTTPClient();

            clientProvider.Received(1).CreateClient(config.HttpClientConfig);
        }

        [Test]
        public void CanGetCurrentTimestamp()
        {
            var expected = 12356789;
            timingProvider.ProvideTimestampInMilliseconds().Returns(expected);

            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            var actual = target.CurrentTimestamp;
                        
            Assert.AreEqual(expected, actual);
            timingProvider.Received(1).ProvideTimestampInMilliseconds();
        }

        [Test]
        public void DefaultSleepTimeIsUsed()
        {
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            target.Sleep();

            timingProvider.Received(1).Sleep(BeaconSendingContext.DEFAULT_SLEEP_TIME_MILLISECONDS);
        }

        [Test]
        public void CanSleepCustomPeriod()
        {
            var expected = 1717;

            var target = new BeaconSendingContext(config, clientProvider, timingProvider);

            target.Sleep(expected);

            timingProvider.Received(1).Sleep(expected);
        }

        [Test]
        public void SessionIsMovedToFinished()
        {
            // given
            var target = new BeaconSendingContext(config, clientProvider, timingProvider);
            // TODO - thomas.grassauer@dynatrace.com - session adds itself ... should we keep this?
            var session = new Session(config, "127.0.0.1", new BeaconSender(target));

            Assert.That(target.GetAllOpenSessions().Count, Is.EqualTo(1));

            // when
            target.FinishSession(session);

            // then
            Assert.That(target.GetAllOpenSessions(), Is.Empty);
            Assert.That(target.GetNextFinishedSession(), Is.SameAs(session));
            Assert.That(target.GetNextFinishedSession(), Is.Null);
        }
    }
}
