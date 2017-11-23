﻿using Dynatrace.OpenKit.Protocol;
using Dynatrace.OpenKit.Providers;

namespace Dynatrace.OpenKit.Core.Communication
{
    /// <summary>
    /// State where no data is captured. Periodically issues a status request to check 
    /// if capuring should be enabled again. The check interval is defined in <code>STATUS_CHECK_INTERVAL</code> 
    /// 
    /// Transitions to
    /// <ul>
    ///     <li><see cref="BeaconSendingCaptureOnState"/> if IsCaputreOn is <code>true</code> and time sync is NOT required</li>
    ///     <li><see cref="BeaconSendingFlushSessionsState"/> on shutdown</li>
    ///     <li><see cref="BeaconSendingTimeSyncState"/> if initial time sync failed</li>
    /// </ul>
    /// </summary>
    internal class BeaconSendingCaptureOffState : AbstractBeaconSendingState
    {
        /// <summary>
        /// Number of retries for the status request
        /// </summary>
        public const int STATUS_REQUEST_RETRIES = 5;
        /// <summary>
        /// Inital sleep time for retries in milliseconds
        /// </summary>
        public const int INITIAL_RETRY_SLEEP_TIME_MILLISECONDS = 1000;

        /// <summary>
        ///  Time period for re-execute of status check
        /// </summary>
        public const int STATUS_CHECK_INTERVAL = 2 * 60 * 60 * 1000;    // wait 2h (in ms) for next status request

        public BeaconSendingCaptureOffState() : base(false) {}

        internal override AbstractBeaconSendingState ShutdownState => new BeaconSendingFlushSessionsState();

        protected override void DoExecute(IBeaconSendingContext context)
        {
            var currentTime = context.CurrentTimestamp;

            var delta = (int) (STATUS_CHECK_INTERVAL - (currentTime - context.LastStatusCheckTime));
            if (delta > 0 && !context.IsShutdownRequested)
            {
                // still have some time to sleep
                context.Sleep(delta);
            }

            // send the status request
            var statusResponse = BeaconSendingRequestUtil.SendStatusRequest(context, STATUS_REQUEST_RETRIES, INITIAL_RETRY_SLEEP_TIME_MILLISECONDS);

            // process the response
            HandleStatusResponse(context, statusResponse);

            // update the last status check time in any case
            context.LastStatusCheckTime = currentTime;
        }

        private static void HandleStatusResponse(IBeaconSendingContext context, StatusResponse statusResponse)
        {
            if (statusResponse != null)
            {
                context.HandleStatusResponse(statusResponse);
            }
            // if initial time sync failed before
            if (context.IsTimeSyncSupported && !TimeProvider.IsTimeSynced)
            {
                // then retry initial time sync
                context.CurrentState = new BeaconSendingTimeSyncState(true);
            }
            else if (statusResponse != null && context.IsCaptureOn)
            {
                // capturing is re-enabled again, but only if we received a response from the server
                context.CurrentState = new BeaconSendingCaptureOnState();
            }
        }
    }
}