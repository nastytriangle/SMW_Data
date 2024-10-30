using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMW_Data
{
    internal class InGameTrackingWebSocket : Qusb2SnesWebSocket
    {        
        public bool IsInGame { get; private set; }
        protected override int CheckFrequency {
            get
            {
                if( !IsConnnected || !IsInGame)
                {
                    return _disconnectedCheckFrequency;
                } else
                {
                    return _connectedCheckFrequency;
                }                
            }            
        }
        public InGameTrackingWebSocket()
        {
            _trackedMemoryAddresses = new List<TrackedMemory>() {
                new (SNESMemoryAddress.InGameIndicator)
            };            
            _connectedCheckFrequency = 1000;
            _disconnectedCheckFrequency = 16;
        }
        protected override void HandleDisconnect()
        {
            IsInGame = false;
        }
        protected override void NoteResponses()
        {            
            IsInGame = _lastResponse == "02";
        }

    }
}
