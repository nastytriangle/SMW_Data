using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMW_Data
{
    internal class SwitchExitTrackingWebSocket : Qusb2SnesWebSocket
    {        
        public int SwitchCount { get; private set; }
        public SwitchExitTrackingWebSocket()
        {
            _trackedMemoryAddresses = new List<TrackedMemory>() {
                new (SNESMemoryAddress.SwitchedActivated)
            };
            ExpectedResponseLength = 8;
            _connectedCheckFrequency = 1000;
            _disconnectedCheckFrequency = 1000;
        }
        protected override void NoteResponses()
        {
                SwitchCount = Enumerable.Range(0, _lastResponse.Length / 2)
                .Sum(i => _lastResponse.Substring(i * 2, 2) != "00" ? 1 : 0);

        }
    }
}
