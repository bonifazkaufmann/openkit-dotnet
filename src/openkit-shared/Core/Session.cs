﻿//
// Copyright 2018 Dynatrace LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using Dynatrace.OpenKit.API;
using Dynatrace.OpenKit.Protocol;
using Dynatrace.OpenKit.Providers;
using System.Threading;

namespace Dynatrace.OpenKit.Core
{

    /// <summary>
    ///  Actual implementation of the ISession interface.
    /// </summary>
    public class Session : ISession
    {
        private static readonly NullRootAction NullRootAction = new NullRootAction();

        // end time of this Session
        private long endTime = -1;

        // Configuration and Beacon reference
        private readonly BeaconSender beaconSender;
        private readonly Beacon beacon;

        // used for taking care to really leave all Actions at the end of this Session
        private SynchronizedQueue<IAction> openRootActions = new SynchronizedQueue<IAction>();


        public Session(BeaconSender beaconSender, Beacon beacon)
        {
            this.beaconSender = beaconSender;
            this.beacon = beacon;

            beaconSender.StartSession(this);
        }

        /// <summary>
        /// Test if this Session is empty or not.
        /// 
        /// A session is considered to be empty, if it does not contain any action or event data.
        /// </summary>
        public bool IsEmpty => beacon.IsEmpty;

        public long EndTime => Interlocked.Read(ref endTime);

        internal bool IsSessionEnded => EndTime != -1;

        // *** ISession interface methods ***

        public void Dispose()
        {
            End();
        }

        public IRootAction EnterAction(string actionName)
        {
            if (!IsSessionEnded)
            {
                return new RootAction(beacon, actionName, openRootActions);
            }

            return NullRootAction;
        }

        public void IdentifyUser(string userTag)
        {
            if (!IsSessionEnded)
            {
                beacon.IdentifyUser(userTag);
            }
        }

        public void ReportCrash(string errorName, string reason, string stacktrace)
        {
            if (!IsSessionEnded)
            {
                beacon.ReportCrash(errorName, reason, stacktrace);
            }
        }

        public void End()
        {
            // check if end() was already called before by looking at endTime
            if (Interlocked.CompareExchange(ref endTime, beacon.CurrentTimestamp, -1L) != -1L)
            {
                return;
            }

            // leave all Root-Actions for sanity reasons
            while (!openRootActions.IsEmpty())
            {
                IAction action = openRootActions.Get();
                action.LeaveAction();
            }

            endTime = beacon.CurrentTimestamp;

            // create end session data on beacon
            beacon.EndSession(this);

            // finish session on configuration and stop managing it
            beaconSender.FinishSession(this);
        }

        // sends the current Beacon state
        public StatusResponse SendBeacon(IHTTPClientProvider clientProvider)
        {
            return beacon.Send(clientProvider);
        }
        
        /// <summary>
        /// Clear captured beacon data.
        /// </summary>
        internal void ClearCapturedData()
        {
            beacon.ClearData();
        }
    }
}
