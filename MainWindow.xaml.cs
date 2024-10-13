using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using SMW_Data.View;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Controls;
using System.Net.Http.Json;

namespace SMW_Data
{    
    public partial class MainWindow : Window
    {
        #region WebsocketTrackedDataVariables
        private int TrackedLevelDeathCounter
        {
            get
            {
                return LevelDeathCount;
            }
            set
            {
                if (_deathTrackingWebSocket != null)
                {
                    _deathTrackingWebSocket.CurrentLevelDeaths = value;
                }
                else
                {
                    LevelDeathCount = value;
                }
            }
        }
        private int TrackedTotalDeathCounter
        {
            get
            {
                return TotalDeathCount;
            }
            set
            {
                if (_deathTrackingWebSocket != null)
                {
                    _deathTrackingWebSocket.TotalDeaths = value;
                }
                else
                {
                    TotalDeathCount = value;
                }
            }
        }
        private bool _isInGame = false;
        private bool _qUSBIsrunning = false;
        private bool QUSBIsRunning
        {
            get
            {
                return _qUSBIsrunning;
            } set
            {
                if (_qUSBIsrunning != value)
                {
                    _qUSBIsrunning = value;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextBlock_QUsb2Snes.Visibility = _qUSBIsrunning ? Visibility.Hidden : Visibility.Visible;
                    });
                }
            }
        }
        private bool IsInGame { 
            get
            {
                return _isInGame;            
            }
            set
            {
                if (_isInGame != value)
                {
                    _isInGame = value;
                    if (_isInGame && TrackInGame)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            StartTimer();
                            //CheckBox_StartTimerAutomatically.IsChecked = false;
                        });
                    }
                }
            }
        }
        private int _SwitchesActivated = 0;
        private int SwitchesActivated
        {
            get
            {
                return _SwitchesActivated;
            }
            set
            {
                if (_SwitchesActivated != value)
                {
                    _SwitchesActivated = value;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextBlock_SwitchCount.Text = $"+{_SwitchesActivated}";
                    });
                }
            }
        }
        private int PreviousExitCounter
        {
            get
            {
                return ExitCount;
            }
            set
            {
                if(_exitCountTrackingWebSocket == null || !_exitCountTrackingWebSocket.IsConnnected)
                {
                    ExitCount = value;
                }
            }
        }
        private int _ExitCount { get; set; }
        private int ExitCount
        {
            get
            {
                return _ExitCount;
            }
            set
            {
                if (value != _ExitCount)
                {
                    int currentExitCount = _ExitCount;
                    _ExitCount = value;
                    if(currentExitCount+ 1 == _ExitCount && timer?.IsEnabled == true)
                    {
                        Button_ManualSplit_Click(null, null);
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextBlock_ExitCountCurrent.Text = value.ToString();
                    });
                }
            }
        }
        private int _totalDeathCount = 0;
        private int TotalDeathCount
        {
            get
            {
                return Int32.Parse(TextBlock_TotalDeathCount.Text);
            }
            set
            {
                if (_totalDeathCount != value)
                {
                    _totalDeathCount = value;
                    IsDirty = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (value < 0)
                        {
                            TextBlock_TotalDeathCount.Text = "0";
                        }
                        else if (value > 999999)
                        {
                            TextBlock_TotalDeathCount.Text = "999999";
                        }
                        else
                        {
                            TextBlock_TotalDeathCount.Text = value.ToString();
                        }
                    });
                }
            }
        }
        private int _levelDeathCount = 0;
        private int LevelDeathCount
        {
            get
            {
                return _levelDeathCount;
            }
            set
            {
                if (_levelDeathCount != value)
                {
                    _levelDeathCount = value;
                    IsDirty = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (value < 0)
                        {
                            TextBlock_LevelDeathCount.Text = "0";
                        }
                        else if (value > 999999)
                        {
                            TextBlock_LevelDeathCount.Text = "999999";
                        }
                        else
                        {
                            TextBlock_LevelDeathCount.Text = value.ToString();
                        }
                    });
                }
            }
        }
        private string _connectedDevice = null;
        private string ConnectedDevice
        {
            set
            {
                if (value != _connectedDevice)
                {
                    _connectedDevice = value;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextBlock_Connection.Text = _connectedDevice == null ? "No Device Found" : "Connected to: " + _connectedDevice;
                        TextBlock_Footer.Text = _connectedDevice == null ? "Not Connected to WebSocket" : "Connected to WebSocket";
                    });
                }
            }
        }
        #endregion
        public int SelectedLevelAccuracyIndex { get; set; } = 1;
        public int SelectedTotalAccuracyIndex { get; set; } = 1;
        public int SelectedDeathImageIndex { get; set; } = 2;

        public BitmapImage NewDeathImage;
        private Timer timerAutoSave;

        private DispatcherTimer timer;        
        private DateTime startTimeLevel;
        private DateTime startTimeTotal;
        private TimeSpan currentTimeLevel = TimeSpan.Zero;
        private TimeSpan currentTimeLastLevel = TimeSpan.Zero;
        private TimeSpan currentTimeTotal = TimeSpan.Zero;
        private TimeSpan elapsedTotal = TimeSpan.Zero;
        private TimeSpan elapsedLevel = TimeSpan.Zero;
        private bool autostartTimer = false;

        static readonly string saveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userdata");      
        protected bool IsDirty { get; set; } = false;
        private Timer _updateDisplayTimer;

        private bool _trackDeaths = false;
        private bool TrackDeaths { get
            {
                return _trackDeaths;
            } set
            {
                if(_trackDeaths != value)
                {
                    _trackDeaths = value;
                    if(_trackDeaths)
                    { 
                        _deathTrackingWebSocket = _deathTrackingWebSocket ?? new DeathTrackingWebSocket();
                        _deathTrackingWebSocket.TotalDeaths = TotalDeathCount;
                        _deathTrackingWebSocket.CurrentLevelDeaths = LevelDeathCount;
                        _deathTrackingWebSocket.StartTimer();
                    } else
                    {
                        _deathTrackingWebSocket?.Stop();
                    }
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        CheckBox_TrackDeaths.IsChecked = _trackDeaths;
                    }));
                }
            }
        }
        private bool _trackInGame = false;
        private bool TrackInGame
        {
            get
            {
                return _trackInGame;
            }
            set
            {
                if (_trackInGame != value)
                {
                    _trackInGame = value;
                    if (_trackInGame)
                    {
                        _inGameTrackingWebSocket = _inGameTrackingWebSocket ?? new InGameTrackingWebSocket();
                        _inGameTrackingWebSocket.StartTimer();
                    }
                    else
                    {
                        _inGameTrackingWebSocket?.Stop();
                    }
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        CheckBox_StartTimerAutomatically.IsChecked = _trackInGame;                        
                    }));
                }
            }
        }
        private bool _trackExits = false;
        private bool TrackExits
        {
            get
            {
                return _trackExits;
            }
            set
            {
                if (_trackExits != value)
                {
                    _trackExits = value;
                    if (_trackExits)
                    {
                        _exitCountTrackingWebSocket = _exitCountTrackingWebSocket ?? new ExitCountTrackingWebSocket();
                        _exitCountTrackingWebSocket.StartTimer();
                    }
                    else
                    {
                        _exitCountTrackingWebSocket?.Stop();
                    }
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        CheckBox_TrackExits.IsChecked = _trackExits;                        
                    }));
                }
            }
        }
        private bool _trackSwitches = false;
        private bool TrackSwitches
        {
            get
            {
                return _trackSwitches;
            }
            set
            {
                if (_trackSwitches != value)
                {
                    _trackSwitches = value;
                    if (_trackSwitches)
                    {
                        _switchExitTrackingWebSocket = _switchExitTrackingWebSocket ?? new SwitchExitTrackingWebSocket();
                        _switchExitTrackingWebSocket.StartTimer();
                    }
                    else
                    {
                        _switchExitTrackingWebSocket?.Stop();
                    }
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        CheckBox_ShowSwitchExits.IsChecked = _trackSwitches;
                        TextBlock_SwitchCount.Visibility = _trackSwitches ? Visibility.Visible : Visibility.Hidden;                        
                    }));
                }
            }
        }
        private DeathTrackingWebSocket _deathTrackingWebSocket = new DeathTrackingWebSocket();
        private InGameTrackingWebSocket _inGameTrackingWebSocket = new InGameTrackingWebSocket();
        private ExitCountTrackingWebSocket _exitCountTrackingWebSocket = new ExitCountTrackingWebSocket();
        private SwitchExitTrackingWebSocket _switchExitTrackingWebSocket = new SwitchExitTrackingWebSocket();           
        public MainWindow()
        {

            InitializeComponent();
            if (!System.IO.Directory.Exists(saveDirectory))
                System.IO.Directory.CreateDirectory(saveDirectory);
            AutoLoad();           
            
            //SetUpWebSockets();            
            Closing += MainWindow_Closing;
            timerAutoSave = new Timer(DoAutoSave, null, 100, Timeout.Infinite);
            _updateDisplayTimer = new Timer(UpdateDisplay, null, 10, Timeout.Infinite);
        }
        private void StartSocket(Qusb2SnesWebSocket socket)
        {
            if(socket is DeathTrackingWebSocket)
            {
                var deathSocket = (DeathTrackingWebSocket)socket;
                deathSocket.TotalDeaths = TotalDeathCount;
                deathSocket.CurrentLevelDeaths = LevelDeathCount;
            }
            socket.StartTimer();
        }        
        private void UpdateDisplay(object? state)
        {
            var websockets = new List<Qusb2SnesWebSocket> { _deathTrackingWebSocket,
                _switchExitTrackingWebSocket,_inGameTrackingWebSocket, _exitCountTrackingWebSocket
            };
            var connected = websockets.FirstOrDefault(ws => ws.IsConnnected && ws.Device != null);
            if (connected != null)
            {
                ConnectedDevice = connected.Device;
            }
            else
            {
                ConnectedDevice = null;
            }
            if (_trackDeaths)
            {                
                TotalDeathCount = _deathTrackingWebSocket.TotalDeaths;
                LevelDeathCount = _deathTrackingWebSocket.CurrentLevelDeaths;
            }
            if(_trackInGame)
            {
               IsInGame = _inGameTrackingWebSocket.IsInGame;
            }    
            if(_trackExits)
            {
                if(IsInGame || !_trackInGame)
                {
                    ExitCount = _exitCountTrackingWebSocket.CurrentExitCount;
                }
            }
            if(_trackSwitches)
            {
                SwitchesActivated = _switchExitTrackingWebSocket.SwitchCount;
            }
            
            QUSBIsRunning = Process.GetProcessesByName("QUsb2Snes").Length != 0;
            

            _updateDisplayTimer.Change(10, Timeout.Infinite);
        }
        private void DoAutoSave(object? state)
        {
            if (IsDirty)
                AutoSave();
            timerAutoSave.Change(100, Timeout.Infinite);
        }
        private void SetUpWebSockets()
        {
            if( _trackDeaths)
            {
                _deathTrackingWebSocket = _deathTrackingWebSocket ?? new DeathTrackingWebSocket();
                _deathTrackingWebSocket.TotalDeaths = TotalDeathCount;
                _deathTrackingWebSocket.CurrentLevelDeaths = LevelDeathCount;
                //websockets.Add(_deathTrackingWebSocket);
            } else
            {
                _deathTrackingWebSocket?.Stop();
                //_deathTrackingWebSocket = null;
            }
            if(_trackInGame)
            {
                _inGameTrackingWebSocket = _inGameTrackingWebSocket ?? new InGameTrackingWebSocket();
                //websockets.Add(_inGameTrackingWebSocket);
            } else
            {
                _inGameTrackingWebSocket?.Stop();
                //_inGameTrackingWebSocket = null;
            }
            if(_trackExits)
            {
                _exitCountTrackingWebSocket = _exitCountTrackingWebSocket ?? new ExitCountTrackingWebSocket();
                //websockets.Add(_exitCountTrackingWebSocket);
            } else
            {
                _exitCountTrackingWebSocket?.Stop();
                //_exitCountTrackingWebSocket = null;
            }
            if (_trackSwitches)
            {
                _switchExitTrackingWebSocket = _switchExitTrackingWebSocket ?? new SwitchExitTrackingWebSocket();
                //websockets.Add(_switchExitTrackingWebSocket);
            } else
            {
                _switchExitTrackingWebSocket?.Stop();
                //_switchExitTrackingWebSocket = null;
            }

            //foreach (var websocket in websockets)
            //    websocket?.StartTimer();
        }
        private void SetPanelColors(Brush brush)
        {
            var panels = new List<System.Windows.Controls.Panel>
            {
                GridMain, GridHackName,GridCreators
            };
            foreach(var panel in panels)
            {
                panel.Background = brush;
            }            
        }
        private void SetControlsForeground(Brush brush)
        {
            var controls = new List<System.Windows.Controls.Control>
            {
                Label_Hack, Label_Creator,Label_LevelDeathCount,Label_TotalDeathCount,
                Label_ExitCount, Label_LevelTime,Label_LastLevelTime,Label_TotalTime,
            };
            foreach(var control in controls)
            {
                control.Foreground = brush;
            }
            var textBlocks = new List<TextBlock> {
                TextBlock_LevelDeathCount, TextBlock_TotalDeathCount, TextBlock_ExitCountCurrent,
                TextBlock_ExitCountSlash,TextBlock_ExitCountTotal,TextBlock_SwitchCount,
                TextBlock_LevelTime,TextBlock_LastLevelTime,TextBlock_TotalTime,
            };
            foreach(var block in textBlocks)
            {
                block.Foreground = brush;
            }            
        }
        private void MenuItem_Click_Colors(object sender, RoutedEventArgs e)
        {
            ColorWindow colorWindow = new(this);
            colorWindow.ShowDialog();
            if (colorWindow.ColorOK)
            {
                SetPanelColors(colorWindow.NewBackgroundColor);
                SetControlsForeground(colorWindow.NewTextColor);
            }
        }
        private void MenuItem_Click_Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Button_SetLevel_Click(object sender, RoutedEventArgs e)
        {
            TrackedLevelDeathCounter = Int32.TryParse(TextBox_LevelDeaths.Text, out int value) ? value : 0;
        }
        private void Button_SetTotal_Click(object sender, RoutedEventArgs e)
        {
            TrackedTotalDeathCounter = Int32.TryParse(TextBox_TotalDeaths.Text, out int value) ? value : 0;
        }
        private void ButtonLevelPlus_Click(object sender, RoutedEventArgs e)
        {
            TrackedLevelDeathCounter++;           
        }
        private void ButtonLevelMinus_Click(object sender, RoutedEventArgs e)
        {
            TrackedLevelDeathCounter--;
        }
        private void ButtonLevelZero_Click(object sender, RoutedEventArgs e)
        {
            TrackedLevelDeathCounter = 0;            
        }
        private void ButtonTotalPlus_Click(object sender, RoutedEventArgs e)
        {
            TrackedTotalDeathCounter++;
        }
        private void ButtonTotalMinus_Click(object sender, RoutedEventArgs e)
        {
            TrackedTotalDeathCounter--;
        }
        private void ButtonTotalZero_Click(object sender, RoutedEventArgs e)
        {
            TrackedTotalDeathCounter = 0;
        }
        private void Button_TimersStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (Button_TimersStartStop.Content.ToString().Contains("Start")) // start timer
            {
                StartTimer();
            }
            else //stop timer
            {
                Button_TimersStartStop.Content = "Start Timer";
                SolidColorBrush limeColor = new SolidColorBrush(Colors.Lime);
                Button_TimersStartStop.Background = limeColor;

                SetManualTimerUIVisibility(Visibility.Visible);                
                timer.Stop();
                TrackInGame = false;    
                IsInGame = false;

                GetCurrentTimeTotal();
                GetCurrentTimeLastLevel();
                GetCurrentTimeLevel();
                UpdateTimeFormats();
            }
        }
        private void SetManualTimerUIVisibility(Visibility visibility)
        {
            var uIElementsToToggle = new List<UIElement> {
                Button_TimerResetAll,
                Button_TimerResetLevel,
                Button_TimerResetLastLevel,
                Button_TimerResetTotal,
                Button_TimersSetAll,
                Button_TimersSetLevel,
                Button_TimersSetLastLevel,
                Button_TimersSetTotal,
                Label_TimeUnits,
                Label_Level,
                Label_LastLevel,
                Label_Total,
                TextBox_LevelHours,
                TextBox_LevelMinutes,
                TextBox_LevelSeconds,
                TextBox_LevelMilliseconds,
                TextBox_LastLevelHours,
                TextBox_LastLevelMinutes,
                TextBox_LastLevelSeconds,
                TextBox_LastLevelMilliseconds,
                TextBox_TotalHours,
                TextBox_TotalMinutes,
                TextBox_TotalSeconds,
                TextBox_TotalMilliseconds,
                };
            foreach(UIElement element in uIElementsToToggle)
            {
                element.Visibility = visibility;
            }            
        }
        private void StartTimer()
        {
            Button_TimersStartStop.Content = "Stop Timer";
            SolidColorBrush redColor = new SolidColorBrush(Colors.Red);
            Button_TimersStartStop.Background = redColor;

            SetManualTimerUIVisibility(Visibility.Hidden);            

            if (currentTimeTotal == TimeSpan.Zero)
            {
                startTimeTotal = DateTime.Now;
                startTimeLevel = startTimeTotal;
                Button_TimerResetLastLevel_Click(this, new RoutedEventArgs());
            }
            else
            {
                startTimeTotal = DateTime.Now - currentTimeTotal;
                startTimeLevel = DateTime.Now - currentTimeLevel;
            }
            if (timer != null)
            {
                timer.Stop();               
            } else
            {
                timer = new DispatcherTimer();
            }            
            timer.Interval = TimeSpan.FromMilliseconds(1);
            timer.Tick += Timer_Main_Tick;
            timer.Start();
        }
        private string TimeSpanToTextPartA(TimeSpan timeSpan)
        {
            if(timeSpan.TotalDays >= 1 || timeSpan.TotalHours >=1)            
                return (int)timeSpan.TotalHours + timeSpan.ToString(@"\:mm\:ss");
            if (timeSpan.TotalMinutes >= 1)
                return timeSpan.ToString(@"m\:ss");
            if (timeSpan.TotalSeconds >= 1)
                return ((int)timeSpan.TotalSeconds).ToString();
            return "0";
        }
        private string TimeSpanToTextPartB(TimeSpan timeSpan, int accuracyIndex)
        {
            if(accuracyIndex == 0)            
                return timeSpan.ToString(@"\.fff");          
            if (accuracyIndex == 1)            
                return timeSpan.ToString(@"\.ff");            
            if (accuracyIndex == 2)            
                return timeSpan.ToString(@"\.f");          
            return "";
        }
        private void PopulateTime(TextBlock textBlock, TimeSpan timeSpan, int accuracyIndex)
        {
            textBlock.Inlines.Clear();
            var partA = TimeSpanToTextPartA(timeSpan);
            var partB = TimeSpanToTextPartB(timeSpan, accuracyIndex);
            if(partB == null)
            {
                textBlock.Text = partA;
            } else
            {
                textBlock.Inlines.Add(new Run(partA));
                textBlock.Inlines.Add(new Run(partB) { FontSize = 18 });
            }            
        }
        private void Timer_Main_Tick(object sender, EventArgs e)
        {
            elapsedTotal = DateTime.Now - startTimeTotal;
            elapsedLevel = DateTime.Now - startTimeLevel;
            PopulateTime(TextBlock_TotalTime, elapsedTotal, SelectedTotalAccuracyIndex);
            PopulateTime(TextBlock_LevelTime, elapsedLevel, SelectedLevelAccuracyIndex);
        }
        private TimeSpan GetTimeSpan(string text)
        {
            var millisplit = text.Split('.');
            decimal milliseconds = 0;
            int seconds = 0;
            int minutes = 0;
            int hours = 0;
            if (millisplit.Length > 1)
            {
                milliseconds = Convert.ToDecimal("." + millisplit[1]);
                text = millisplit[0];
            }
            var hmsSplit = text.Split(':');
            var segments = hmsSplit.Length;
            switch (segments)
            {
                case 3:
                    {
                        seconds = int.Parse(hmsSplit[2]);
                        minutes = int.Parse(hmsSplit[1]);
                        hours = int.Parse(hmsSplit[0]);
                        break;
                    }

                case 2:
                    {
                        seconds = int.Parse(hmsSplit[1]);
                        minutes = int.Parse(hmsSplit[0]);
                        break;
                    }
                case 1:
                    {
                        seconds = int.Parse(hmsSplit[0]);
                        break;
                    }
            }
            return new TimeSpan(0, hours, minutes, seconds, (int)(milliseconds * 1000));                            
        }
        private void GetCurrentTimeTotal()
        {
            currentTimeTotal = GetTimeSpan(TextBlock_TotalTime.Text);
        }
        private void GetCurrentTimeLastLevel()
        {
            currentTimeLastLevel = GetTimeSpan(TextBlock_LastLevelTime.Text);
        }
        private void GetCurrentTimeLevel()
        {
            currentTimeLevel = GetTimeSpan(TextBlock_LevelTime.Text);
        }
        private void UpdateTimeText(TextBlock textBlock, TimeSpan timeSpan, int accuracyIndex, int fontSize = 18) {
            string formatted = FormatTime(timeSpan, accuracyIndex);
            textBlock.Inlines.Clear();
            string[] timeParts = formatted.Split('.');
            textBlock.Inlines.Add(new Run(timeParts[0]));
            if(timeParts.Length > 1)
            {
                textBlock.Inlines.Add(new Run("." + timeParts[1]) { FontSize = fontSize });
            }
        }
        public void UpdateTimeFormats()
        {
            UpdateTimeText(TextBlock_LevelTime, currentTimeLevel, SelectedLevelAccuracyIndex);
            UpdateTimeText(TextBlock_LastLevelTime, currentTimeLastLevel, SelectedLevelAccuracyIndex, 12);
            UpdateTimeText(TextBlock_TotalTime, currentTimeTotal, SelectedTotalAccuracyIndex);            
        }
        private string FormatTime(TimeSpan time, int accuracyIndex)
        {
            return TimeSpanToTextPartA(time) + TimeSpanToTextPartB(time, accuracyIndex);
        }
        private void Button_TimerResetLevel_Click(object sender, RoutedEventArgs e)
        {
            startTimeLevel = DateTime.Now;
            currentTimeLevel = TimeSpan.Zero;
            UpdateTimeText(TextBlock_LevelTime, currentTimeLevel, SelectedLevelAccuracyIndex);            
        }
        private void Button_TimerResetLastLevel_Click(object sender, RoutedEventArgs e)
        {
            currentTimeLastLevel = TimeSpan.Zero;
            UpdateTimeText(TextBlock_LastLevelTime, currentTimeLastLevel, SelectedLevelAccuracyIndex, 12);            
        }
        private void Button_TimerResetTotal_Click(object sender, RoutedEventArgs e)
        {
            startTimeTotal = DateTime.Now;
            startTimeLevel = DateTime.Now;
            currentTimeTotal = TimeSpan.Zero;
            UpdateTimeText(TextBlock_TotalTime, currentTimeTotal, SelectedTotalAccuracyIndex);            
        }
        private void Button_TimerResetAll_Click(object sender, RoutedEventArgs e)
        {
            Button_TimerResetLevel_Click(this, new RoutedEventArgs());
            Button_TimerResetLastLevel_Click(this, new RoutedEventArgs());
            Button_TimerResetTotal_Click(this, new RoutedEventArgs());
        }
        private void Button_TimersSetLevel_Click(object sender, RoutedEventArgs e)
        {
            currentTimeLevel = new TimeSpan(0, int.Parse(TextBox_LevelHours.Text),
                int.Parse(TextBox_LevelMinutes.Text),
                int.Parse(TextBox_LevelSeconds.Text),
                int.Parse(TextBox_LevelMilliseconds.Text));
            UpdateTimeText(TextBlock_LevelTime, currentTimeLevel, SelectedLevelAccuracyIndex);            
        }
        private void Button_TimersSetLastLevel_Click(object sender, RoutedEventArgs e)
        {
            currentTimeLastLevel = new TimeSpan(0, int.Parse(TextBox_LastLevelHours.Text), 
                            int.Parse(TextBox_LastLevelMinutes.Text), 
                            int.Parse(TextBox_LastLevelSeconds.Text), 
                            int.Parse(TextBox_LastLevelMilliseconds.Text));           
            UpdateTimeText(TextBlock_LastLevelTime, currentTimeLastLevel, SelectedLevelAccuracyIndex, 12);            
        }
        private void Button_TimersSetTotal_Click(object sender, RoutedEventArgs e)
        {
            currentTimeTotal = new TimeSpan(0, int.Parse(TextBox_TotalHours.Text),
                            int.Parse(TextBox_TotalMinutes.Text),
                            int.Parse(TextBox_TotalSeconds.Text),
                            int.Parse(TextBox_TotalMilliseconds.Text));
            UpdateTimeText(TextBlock_TotalTime, currentTimeTotal, SelectedTotalAccuracyIndex);            
        }
        private void Button_TimersSetAll_Click(object sender, RoutedEventArgs e)
        {
            Button_TimersSetLevel_Click(this, new RoutedEventArgs());
            Button_TimersSetLastLevel_Click(this, new RoutedEventArgs());
            Button_TimersSetTotal_Click(this, new RoutedEventArgs());
        }
        private void Button_SetTotalExits_Click(object sender, RoutedEventArgs e)
        {
            TextBlock_ExitCountTotal.Text = TextBox_ExitCountTotal_Manual.Text;
        }
        private void Button_SetCurrentExits_Click(object sender, RoutedEventArgs e)
        {
            TextBlock_ExitCountCurrent.Text = TextBox_ExitCountCurrent_Manual.Text;
        }
        private void CheckBox_ShowSwitchExits_Toggle(object sender, RoutedEventArgs e)
        {
            TrackSwitches = CheckBox_ShowSwitchExits.IsChecked == true;
        }
        private void CheckBox_TrackDeaths_Toggle(object sender, RoutedEventArgs e)
        {
            TrackDeaths = CheckBox_TrackDeaths.IsChecked == true;            
        }
        private void CheckBox_TrackExits_Toggle(object sender, RoutedEventArgs e)
        {
            TrackExits = CheckBox_TrackExits.IsChecked == true;            
        }

        private void TextBox_HackName_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TextBox_HackName.Text == "[Enter Hack Name Here]")
            {
                TextBox_HackName.Clear();
                TextBox_HackName.Focus();
                TextBox_HackName.Foreground = new SolidColorBrush(Colors.Black);
            }
        }
        private void TextBox_CreatorName_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TextBox_CreatorName.Text == "[Optional Author]")
            {
                TextBox_CreatorName.Clear();
                TextBox_CreatorName.Focus();
                TextBox_CreatorName.Foreground = new SolidColorBrush(Colors.Black);
            }
        }
        private void MenuItem_Click_Timers(object sender, RoutedEventArgs e)
        {
            TimerWindow timerWindow = new(this);
            timerWindow.ShowDialog();
            if (timerWindow.TimerOK)
            {
                UpdateTimeFormats();
            }
        }
        private void MenuItem_Click_DeathImage(object sender, RoutedEventArgs e)
        {
            DeathImageWindow deathImageWindow = new(this);
            deathImageWindow.ShowDialog();
            if (deathImageWindow.DeathImageOK)
            {
                SelectedDeathImageIndex = deathImageWindow.ComboBoxDeathImage.SelectedIndex;
                UpdateDeathImage();
            }
        }
        private void Button_ManualSplit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                TrackedLevelDeathCounter = 0;                
                UpdateTimeText(TextBlock_LastLevelTime, GetTimeSpan(TextBlock_LevelTime.Text), SelectedLevelAccuracyIndex, 12);                                
                UpdateTimeText(TextBlock_LevelTime, TimeSpan.Zero, SelectedLevelAccuracyIndex);
            });            
            startTimeLevel = DateTime.Now;
        }
        private void MenuItem_Click_Fonts(object sender, RoutedEventArgs e)
        {
            FontsWindow fontsWindow = new(this);
            fontsWindow.ShowDialog();
            if (fontsWindow.FontsOK)
            {
                Label_Hack.FontFamily = fontsWindow.NewFontTitle;
                Label_Creator.FontFamily = fontsWindow.NewFontAuthor;
            }
        }
        private void PreviewTextInt(object sender, TextCompositionEventArgs e)
        {
            if (!int.TryParse(e.Text, out _))
            {
                e.Handled = true;
            }
        }
        private void txtLostFocusThreeZeroes(object sender, RoutedEventArgs e)
        {
            var box = (TextBox)sender;
            if (!Int32.TryParse(box.Text,out _))
            {
                box.Text = "000";
            }            
        }
        private void txtLostFocusOneZero(object sender, RoutedEventArgs e)
        {
            var box = (TextBox)sender;
            if (!Int32.TryParse(box.Text, out _))
            {
                box.Text = "0";
            }            
        }
        private void txtLostFocusTwoZeroes(object sender, RoutedEventArgs e)
        {
            var box = (TextBox)sender;
            if (!Int32.TryParse(box.Text, out int value) || value == 0)                
            {
                box.Text = "00";
            }
            else if(value > 59)
            {
                box.Text = "59";                
            }
        }        
        private void txtBlankIfZeroFocus(object sender, RoutedEventArgs e)
        {
            var box = (TextBox)sender;
            if(!Int32.TryParse(box.Text, out int response) || response == 0)            
                box.Clear();            
            box.Focus();
        }
        private void MenuItem_Click_Save_Hack(object sender, RoutedEventArgs e)
        {
            SaveData($"{Label_Hack.Content}.json");
        }
        private void MenuItem_Click_Load_Hack(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                FileName = "savedata",
                DefaultExt = ".json",
                Filter = "Json (.json)|*.json",
                InitialDirectory = saveDirectory
            };
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                LoadData(dialog.FileName);
            }
        }
        private void SaveData(string fileName)
        {         
            Application.Current.Dispatcher.Invoke(() =>
            {
                SaveData data = new SaveData
                {
                    LevelTime = TextBlock_LevelTime.Text,
                    LastLevelTime = TextBlock_LastLevelTime.Text,
                    TotalTime = TextBlock_TotalTime.Text,
                    LevelTimerAccuracy = SelectedLevelAccuracyIndex,
                    TotalTimerAccuracy = SelectedTotalAccuracyIndex,
                    HackName = Label_Hack.Content.ToString(),
                    LevelDeaths = LevelDeathCount,
                    TotalDeaths = TotalDeathCount,
                    BackgroundColor = (SolidColorBrush)GridMain.Background,
                    TextColor = (SolidColorBrush)Label_LevelDeathCount.Foreground,
                    DeathImage = SelectedDeathImageIndex,
                    TitleFont = Label_Hack.FontFamily,
                    AuthorFont = Label_Creator.FontFamily,
                    Creator = Label_Creator.Content.ToString(),
                    PreviousExitCounter = ExitCount,
                    ExitCountTotal = TextBlock_ExitCountTotal.Text,
                    TrackDeaths = TrackDeaths,
                    TrackExits = TrackExits,
                    TrackInGame = TrackInGame,
                    TrackSwitches = TrackSwitches
                };
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(Path.Combine(saveDirectory, fileName), json);
                File.WriteAllText(Path.Combine(saveDirectory, "currentTotalDeathCount.txt"), data.TotalDeaths.ToString());
                File.WriteAllText(Path.Combine(saveDirectory, "currentLevelDeathCount.txt"), data.LevelDeaths.ToString());
                File.WriteAllText(Path.Combine(saveDirectory, "currentHackName.txt"), data.HackName);
                File.WriteAllText(Path.Combine(saveDirectory, "currentAuthors.txt"), Label_Creator.Content.ToString());
            });
        }       
        private void AutoSave()
        {
            SaveData("autosave.json");  
            IsDirty = false;
        }
        private void AutoLoad()
        {           
            string filePath = Path.Combine(saveDirectory, "autosave.json");
            if (File.Exists(filePath))
            {
                LoadData("autosave.json");                
            }
        }
        private void LoadData(string filename = "defaultdata.json")
        {
            string filePath = Path.Combine(saveDirectory, filename);
            if(!File.Exists(filePath))
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    FileName = "savedata",
                    DefaultExt = ".json",
                    Filter = "Json (.json)|*.json",
                    InitialDirectory = saveDirectory
                };
                bool? result = dialog.ShowDialog();
                if(result == true)
                {
                    filePath = dialog.FileName;
                }
            }            
            if (File.Exists(filePath)) { 
                string json = File.ReadAllText(filePath);
                var data = JsonConvert.DeserializeObject<SaveData>(json);
            
                //Update Timers & Accuracy
                TextBlock_LevelTime.Text = data.LevelTime;
                TextBlock_LastLevelTime.Text = data.LastLevelTime;
                TextBlock_TotalTime.Text = data.TotalTime;
                SelectedLevelAccuracyIndex = data.LevelTimerAccuracy;
                SelectedTotalAccuracyIndex = data.TotalTimerAccuracy;
                PreviousExitCounter = data.PreviousExitCounter;

                GetCurrentTimeLevel();
                GetCurrentTimeLastLevel();
                GetCurrentTimeTotal();
                UpdateTimeFormats();

                //Update Hack Data
                Label_Hack.Content = data.HackName;
                Label_Creator.Content = data.Creator;
                TextBox_HackName.Text = data.HackName;
                TextBlock_ExitCountTotal.Text = data.ExitCountTotal;

                //Update Death Counts
                LevelDeathCount = data.LevelDeaths;
                TotalDeathCount = data.TotalDeaths;

                //Update Colors
                SetPanelColors(data.BackgroundColor);
                SetControlsForeground(data.TextColor);

                //Update Death Image            
                SelectedDeathImageIndex = data.DeathImage;
                UpdateDeathImage();

                //Update Fonts
                Label_Hack.FontFamily = data.TitleFont;
                Label_Creator.FontFamily = data.AuthorFont;

                TrackSwitches = data.TrackSwitches;
                TrackInGame = data.TrackInGame;
                TrackDeaths = data.TrackDeaths;
                TrackExits = data.TrackExits;
            }
        }
        private void UpdateDeathImage()
        {
            switch (SelectedDeathImageIndex)
            {
                case 0:
                    NewDeathImage = new BitmapImage(new Uri("pack://application:,,,/images/SMB1.png"));
                    break;
                case 1:
                    NewDeathImage = new BitmapImage(new Uri("pack://application:,,,/images/SMB3.png"));
                    break;
                case 2:
                    NewDeathImage = new BitmapImage(new Uri("pack://application:,,,/images/SMW.png"));
                    break;
                case 3:
                    NewDeathImage = new BitmapImage(new Uri("pack://application:,,,/images/Paper Mario.png"));
                    break;
            }
            Image_MarioDeath1.Source = NewDeathImage;
            Image_MarioDeath2.Source = NewDeathImage;
        }
        private void MenuItem_Click_Clear(object sender, RoutedEventArgs e)
        {
            string filePath = Path.Combine(saveDirectory, "defaultdata.json");
            string json = JsonConvert.SerializeObject(new SaveData(), Formatting.Indented);
            File.WriteAllText(filePath, json);
            LoadData();
            TextBox_HackName.Text = "[Enter Hack Name Here]";
            TextBox_HackName.Foreground = new SolidColorBrush(Colors.DarkGray);
            TextBox_CreatorName.Text = "[Optional Author]";
            TextBox_CreatorName.Foreground = new SolidColorBrush(Colors.DarkGray);
            Label_Hack.Content = "HackName";
            Label_Creator.Content = "Author";
        }
        private void CheckBox_AutoStartTimer_Toggle(object sender, RoutedEventArgs e)
        {
            TrackInGame = CheckBox_StartTimerAutomatically.IsChecked == true;
        }
        //private void CheckBox_AutoStartTimer_Checked(object sender, RoutedEventArgs e)
        //{
        //    autostartTimer = true;
        //}
        //private void CheckBox_AutoStartTimer_Unchecked(object sender, RoutedEventArgs e)
        //{
        //    autostartTimer = false;
        //}
        void MainWindow_Closing(object sender, CancelEventArgs e) 
        {
            AutoSave();
        }
        #region SMWCentralData TODO Refactor maybe
        private async void Button_UpdateHackInfo_Click(object sender, RoutedEventArgs e)
        {
            string hackName = TextBox_HackName.Text;            
            string[] hackData = await SMWCentralAPICall(hackName);

            if (hackData[0] == "Cannot find Hack Name")
            {
                Label_Hack.Content = hackName;
                Label_Creator.Content = $"By: {TextBox_CreatorName.Text}";
                TextBlock_ExitCountTotal.Text = "??";
            }
            else
            {
                Label_Hack.Content = hackData[0];
                Label_Creator.Content = "By: " + hackData[1];
                TextBlock_ExitCountTotal.Text = hackData[2];
            }
        }
        private async void Button_GetHackData_Click(object sender, RoutedEventArgs e)
        {
            string hackName = TextBox_HackName.Text;
            string[] hackData = await SMWCentralAPICall2(hackName);

            if (hackData[0] == "Cannot find Hack Name")
            {
                MessageBox.Show("Cannot find Hack Name");
            }
            else
            {
                MessageBox.Show($"Hack Name: {hackData[0]} \n" +
                    $"Hack ID: {hackData[1]} \n" +
                    $"Hack Section: {hackData[2]} \n" +
                    $"Date Submitted: {hackData[3]} \n" +
                    $"Moderated: {hackData[4]} \n" +
                    $"Authors: {hackData[5]} \n" +
                    $"Tags: {hackData[6]} \n" +
                    $"Rating: {hackData[7]} \n" +
                    $"Downloads: {hackData[8]} \n" +
                    $"Length: {hackData[9]} \n" +
                    $"Difficulty: {hackData[10]} \n\n" +
                    $"Description: \n{hackData[11]}");
            }
        }
        static async Task<string[]> SMWCentralAPICall(string hackName)
        {
            var hackData = new List<string>();
            hackData.Add("Cannot find Hack Name");

            string lengthText = "??";
            string apiUrl = $"https://www.smwcentral.net/ajax.php?a=getsectionlist&s=smwhacks&u=0&f[name]={hackName}";

            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage response = await httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();  // Read and parse the JSON response
                    JObject jsonObject = JObject.Parse(jsonContent);                  // Parse the JSON into a JObject

                    if (jsonObject["data"] != null)                                   // Check if the JSON response contains data
                    {
                        JArray data = (JArray)jsonObject["data"];
                        foreach (JToken item in data)
                        {
                            string name = item["name"].ToString();
                            if (name.ToLower() == hackName.ToLower())
                            {
                                hackData.Clear();
                                string hack_Name = name;

                                string hackAuthorsArray = item["authors"].ToString();                   //user[]
                                string[] authorsArray = JArray.Parse(hackAuthorsArray).Select(author => author["name"].ToString()).ToArray();
                                string hackAuthors = string.Join(", ", authorsArray);

                                string length = item["fields"]["length"].ToString();
                                lengthText = length.Replace(" exit(s)", string.Empty).Trim();
                                hackData.AddRange(new string[] { hack_Name, hackAuthors, lengthText });
                                break;
                            }
                        }
                    }
                }
            }

            if (lengthText == "??")
            {
                apiUrl = $"https://www.smwcentral.net/ajax.php?a=getsectionlist&s=smwhacks&u=1&f[name]={hackName}";

                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage response = await httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonContent = await response.Content.ReadAsStringAsync();  // Read and parse the JSON response
                        JObject jsonObject = JObject.Parse(jsonContent);                  // Parse the JSON into a JObject

                        if (jsonObject["data"] != null)                                   // Check if the JSON response contains data
                        {
                            JArray data = (JArray)jsonObject["data"];
                            foreach (JToken item in data)
                            {
                                string name = item["name"].ToString();
                                if (name.ToLower() == hackName.ToLower())
                                {
                                    hackData.Clear();
                                    string hack_Name = name;

                                    string hackAuthorsArray = item["authors"].ToString();                   //user[]
                                    string[] authorsArray = JArray.Parse(hackAuthorsArray).Select(author => author["name"].ToString()).ToArray();
                                    string hackAuthors = string.Join(", ", authorsArray);

                                    string length = item["fields"]["length"].ToString();
                                    lengthText = length.Replace(" exit(s)", string.Empty).Trim();
                                    hackData.AddRange(new string[] { hack_Name, hackAuthors, lengthText });
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return hackData.ToArray();
        }
        static async Task<string[]> SMWCentralAPICall2(string hackName)
        {
            List<string> hackData = new List<string>();
            hackData.Add("Cannot find Hack Name");

            string apiUrl = $"https://www.smwcentral.net/ajax.php?a=getsectionlist&s=smwhacks&u=0&f[name]={hackName}";

            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage response = await httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();  // Read and parse the JSON response
                    JObject jsonObject = JObject.Parse(jsonContent);                  // Parse the JSON into a JObject

                    if (jsonObject["data"] != null)                                   // Check if the JSON response contains data
                    {
                        JArray data = (JArray)jsonObject["data"];
                        foreach (JToken item in data)
                        {
                            string name = item["name"].ToString();
                            if (name.ToLower() == hackName.ToLower())
                            {
                                hackData.Clear();
                                string hack_Name = name;
                                string hackID = item["id"].ToString();                                  //int
                                string hackSection = item["section"].ToString();                        //string

                                string hackTimeUNIX = item["time"].ToString();
                                string hackTime = null;
                                if (long.TryParse(hackTimeUNIX, out long unixTimestamp))
                                {
                                    DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
                                    hackTime = dateTime.ToString();
                                }

                                string hackModerated = item["moderated"].ToString();                    //bool

                                string hackAuthorsArray = item["authors"].ToString();                   //user[]
                                string[] authorsArray = JArray.Parse(hackAuthorsArray).Select(author => author["name"].ToString()).ToArray();
                                string hackAuthors = string.Join(", ", authorsArray);

                                string hackTagsArray = item["tags"].ToString();                         //string[]
                                string[] tagsArray = JArray.Parse(hackTagsArray).Select(tag => tag.ToString()).ToArray();
                                string hackTags = string.Join(", ", tagsArray);

                                string hackRating = item["rating"].ToString();                          //number | null

                                string hackDownloads = item["downloads"].ToString();                    //number

                                string hackLength = item["fields"]["length"].ToString();                //string

                                string hackDifficulty = item["fields"]["difficulty"].ToString();        //string

                                string hackDescriptionMessy = item["fields"]["description"].ToString(); //string
                                var doc = new HtmlDocument();
                                doc.LoadHtml(hackDescriptionMessy);
                                doc.DocumentNode.SelectNodes("//br")?.ToList().ForEach(br => br.Remove());
                                string hackDescription = doc.DocumentNode.InnerText;

                                hackData.AddRange(new string[] { hack_Name, hackID, hackSection, hackTime, hackModerated, hackAuthors, hackTags, hackRating, hackDownloads, hackLength, hackDifficulty, hackDescription });
                                break;
                            }
                        }
                    }
                }
            }

            if (hackData[0] == "Cannot find Hack Name")
            {
                apiUrl = $"https://www.smwcentral.net/ajax.php?a=getsectionlist&s=smwhacks&u=1&f[name]={hackName}";

                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage response = await httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonContent = await response.Content.ReadAsStringAsync();  // Read and parse the JSON response
                        JObject jsonObject = JObject.Parse(jsonContent);                  // Parse the JSON into a JObject

                        if (jsonObject["data"] != null)                                   // Check if the JSON response contains data
                        {
                            JArray data = (JArray)jsonObject["data"];
                            foreach (JToken item in data)
                            {
                                string name = item["name"].ToString();
                                if (name.ToLower() == hackName.ToLower())
                                {
                                    hackData.Clear();
                                    string hackID = item["id"].ToString();                                  //int
                                    string hackSection = item["section"].ToString();                        //string

                                    string hackTimeUNIX = item["time"].ToString();
                                    string hackTime = null;
                                    if (long.TryParse(hackTimeUNIX, out long unixTimestamp))
                                    {
                                        DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
                                        hackTime = dateTime.ToString();
                                    }

                                    string hackModerated = item["moderated"].ToString();                    //bool

                                    string hackAuthorsArray = item["authors"].ToString();                   //user[]
                                    string[] authorsArray = JArray.Parse(hackAuthorsArray).Select(author => author["name"].ToString()).ToArray();
                                    string hackAuthors = string.Join(", ", authorsArray);

                                    string hackTagsArray = item["tags"].ToString();                         //string[]
                                    string[] tagsArray = JArray.Parse(hackTagsArray).Select(tag => tag.ToString()).ToArray();
                                    string hackTags = string.Join(", ", tagsArray);

                                    string hackRating = item["rating"].ToString();                          //number | null

                                    string hackDownloads = item["downloads"].ToString();                    //number

                                    string hackLength = item["fields"]["length"].ToString();                //string

                                    string hackDifficulty = item["fields"]["difficulty"].ToString();        //string

                                    string hackDescriptionMessy = item["fields"]["description"].ToString(); //string
                                    var doc = new HtmlDocument();
                                    doc.LoadHtml(hackDescriptionMessy);
                                    doc.DocumentNode.SelectNodes("//br")?.ToList().ForEach(br => br.Remove());
                                    string hackDescription = doc.DocumentNode.InnerText;

                                    hackData.AddRange(new string[] { hackID, hackSection, hackTime, hackModerated, hackAuthors, hackTags, hackRating, hackDownloads, hackLength, hackDifficulty, hackDescription });
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return hackData.ToArray();
        }
        #endregion
    }
}