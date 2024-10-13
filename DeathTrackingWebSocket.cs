using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace SMW_Data
{
    internal class DeathTrackingWebSocket :Qusb2SnesWebSocket
    {        
        public int CurrentLevelDeaths { get; set; }
        public int TotalDeaths { get; set; }
        public DeathTrackingWebSocket() 
        {
            _trackedMemoryAddresses = new List<TrackedMemory>() {
                new (SNESMemoryAddress.DeathCheck)
            };            
            _connectedCheckFrequency = 16;
            _disconnectedCheckFrequency = 500;
        }
        protected override void NoteResponses()
        {
                if (_lastResponse == "09")
                {
                    CurrentLevelDeaths++;
                    TotalDeaths++;
                }
        }
    }
}
