using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMW_Data
{
    enum SNESMemoryAddress
    {
        DeathCheck = 0x7E0071,
        ExitCounter = 0x7E1F2E,
        SwitchedActivated = 0x7E1F27,
        InGameIndicator = 0x7E1F15,
        ButtonsDown = 0x7E0015
    }

    internal class TrackedMemory
    {
        const int ADJUSTMENT = 0x770000; //0xF50000 - 0x7E0000
        public SNESMemoryAddress Address;
        public string RequestAddress()
        {
            return ((int)Address + ADJUSTMENT).ToString("X");
        }
        private int _byteLength = 1;
        public int ByteLength
        {
            get { return _byteLength; }
        }
        public int ResponseLength
        {
            get { return _byteLength * 2; }
        }

        public string LastResponse { get; internal set; }

        public TrackedMemory(SNESMemoryAddress address) {
            Address = address;
            switch (Address)
            {
                case SNESMemoryAddress.ButtonsDown:                                        
                case SNESMemoryAddress.SwitchedActivated:
                    _byteLength =  4;
                    break;
            }
        }
        public string[] AsOperands()
        {
            return new string[] { RequestAddress(), _byteLength.ToString() };
        }
    }
}
