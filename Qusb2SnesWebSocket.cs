using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Diagnostics;
//using System.Windows.Forms;

namespace SMW_Data
{
    internal abstract class Qusb2SnesWebSocket
    {
        protected string _lastResponse = null;
        protected List<TrackedMemory> _trackedMemoryAddresses = null;
        protected readonly bool _Debug;
        protected int ExpectedResponseLength = 2;
        protected string _addressRequest = null;
        protected int _connectedCheckFrequency;
        protected int _disconnectedCheckFrequency;
        private DateTime _lastOpenedTime;
        protected DateTime _LastMessageTime = DateTime.MinValue;
        protected virtual int CheckFrequency
        {
            get
            {
                if( IsConnnected)
                {
                        return _connectedCheckFrequency;   
                } else
                {
                    return _disconnectedCheckFrequency;
                }                
            }
        }
        public Qusb2SnesWebSocket()
        {

        }
        protected string AddressRequest
        {
            get
            {
                if (_addressRequest == null)
                    _addressRequest = GetAddressRequest();
                return _addressRequest;
            }
        }
        private WebSocket ws = null;
        private StringBuilder sb = new StringBuilder();
        private bool waiting = false;
        private string lastResponse { get; set; }
        public string Device { get; set; }
        private bool _isConnecting = false;
        private DateTime _lastConnectTime = DateTime.MinValue;
        private string _previousMessage { get; set; }
        public bool IsConnnected { get
            {
                return Device != null;
            } 
        }
        public System.Threading.Timer Timer { get; internal set; }
        protected void HandleMessage(object? sender, MessageEventArgs e)
        {
            var state = ws.ReadyState;
            _LastMessageTime = DateTime.Now;
            if (Device == null && waiting)
            {
                var messageCheckForDevice = JsonConvert.DeserializeObject<dynamic>(e.Data);
                if (messageCheckForDevice.error == "malformed command")
                {
                    waiting = false;
                    //_isConnecting = false;
                }
                else
                {
                    var devices = messageCheckForDevice.Results.ToObject<string[]>();
                    if (devices != null && devices.Length > 0)
                    {
                        Device = devices[0].ToString();
                        AttachDevice();                        
                    } else
                    {
                        waiting = false;
                        //_isConnecting = false;
                    }
                }
            }
            else if (waiting)
            {
                
                var message = BitConverter.ToString(e.RawData).Replace("-", "");
                sb.Append(message);
                if (sb.Length == ExpectedResponseLength)
                {
                    var lastResponse = sb.ToString();
                    if (_Debug)// && lastResponse != _previousMessage)
                    {
                        _previousMessage = lastResponse;
                        System.Diagnostics.Debug.WriteLine($"{DateTime.Now.ToString("hh.mm.ss.ffffff")}  {lastResponse}");
                    }
                    if (ResponseChanged(lastResponse)){
                        NoteResponses();
                    }
                    
                    sb.Clear();
                    waiting = false;
                }
            }
        }
        protected abstract void NoteResponses();       
        private void HandleOpen(object? sender, EventArgs e)
        {
            _lastOpenedTime = DateTime.Now;
            var deviceListRequest = new
            {
                Opcode = "DeviceList",
                Space = "SNES"
            };
            waiting = true;
            ws.Send(JsonConvert.SerializeObject(deviceListRequest));
        }
        private void HandleClose(object? sender, CloseEventArgs e) {                        
            Device = null;
        }
        public void Initialize()
        {
            if(ws != null)
                ((IDisposable)ws).Dispose(); 
            ws = new WebSocket("ws://localhost:8080");
            ws.OnOpen += HandleOpen;
            ws.OnMessage += HandleMessage;
            ws.OnClose += HandleClose;
            var state = ws.ReadyState;
            _isConnecting = true;
            _lastConnectTime = DateTime.Now;
            try
            {
                ws.Connect();
            } catch (Exception ex)
            {
                var ex2 = ex;
                //Sometimes this fails, not worth handling.
            }            
        }
        private void AttachDevice()
        {
            var attachRequest = new
            {
                Opcode = "Attach",
                Space = "SNES",
                Operands = new[] { Device }
            };
            try
            {
                var request = JsonConvert.SerializeObject(attachRequest);                
                ws.Send(request);
                _isConnecting = false;
                waiting = false;
            }
            catch (Exception ex)
            {
                //var ex2 = ex;
            }
        }
        private string[] GetOperands()
        {
            var operands = new List<String>();
            foreach(var address in _trackedMemoryAddresses)
            {
                operands.AddRange(address.AsOperands());
            }
            return operands.ToArray();
        }
        private string GetAddressRequest()
        {
            var getAddressRequestObject = new
            {
                Opcode = "GetAddress",
                Space = "SNES",
                Operands = GetOperands()
            };
            return JsonConvert.SerializeObject(getAddressRequestObject);
        }
        public void StartTimer()
        {
            Timer = new Timer(TimerTick, null, CheckFrequency, Timeout.Infinite);
        }
        protected void TimerTick(object? state)
        {
            if (Timer != null)
            {
                Stopwatch watch = new Stopwatch();
                RequestData();                
                Timer.Change(Math.Max(0, CheckFrequency - watch.ElapsedMilliseconds), Timeout.Infinite);
            }
        }
        public void RequestData()
        {
            switch (ws?.ReadyState)
            {
                case WebSocketState.Open:
                    if (Device != null)
                    {
                        ws.Send(AddressRequest);
                        waiting = true;
                    } else if(!waiting)
                    {
                        HandleOpen(null, null);
                    }
                    break;
                case WebSocketState.Closed:
                case WebSocketState.Closing:
                case null:
                    Initialize();
                    break;
                case WebSocketState.Connecting:
                    break;

            }
        }
        internal void Stop()
        {
            if(ws != null)
            {
                ((IDisposable)ws).Dispose();
            }
            Timer = null;
        }
        protected bool ResponseChanged(string response)
        {
            if (_lastResponse != null && response == _lastResponse)
            {
                return false;
            }
            _lastResponse = response;
            return true;
        }
    }
}