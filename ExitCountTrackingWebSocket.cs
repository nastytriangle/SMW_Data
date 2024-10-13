using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMW_Data
{
    internal class ExitCountTrackingWebSocket : Qusb2SnesWebSocket
    {        
        public int CurrentExitCount { get; private set; }
        public ExitCountTrackingWebSocket()
        {
            _trackedMemoryAddresses = new List<TrackedMemory>() {
                new (SNESMemoryAddress.ExitCounter)
            };            
            _connectedCheckFrequency = 1000;
            _disconnectedCheckFrequency = 1000;
        }
        protected override void NoteResponses()
        {            
            CurrentExitCount = Convert.ToInt32(_lastResponse, 16);
        }

    }
}
