﻿using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SMW_Data.View;
using WebSocketSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows.Input;
using System.Linq;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SMW_Data
{
    public partial class MainWindow : Window
    {
        public SolidColorBrush CurrentBackgroundColor { get; set; }
        public SolidColorBrush CurrentTextColor { get; set; }

        private static int TotalDeathCount;
        private static int LevelDeathCount;
        public bool DeathState;

        static WebSocket ws;
        private DispatcherTimer timer;

        public string MemoryAddressValue_DeathCheck;
        static readonly int MemoryAddress_DeathCheck = 0x7E0071;
        static readonly int adjMemoryAddress_DeathCheck = 0xF50000 + (MemoryAddress_DeathCheck - 0x7E0000);
        static readonly string AdjustedMemoryAddress_DeathCheck = adjMemoryAddress_DeathCheck.ToString("X");

        public string MemoryAddressValue_KeyExit;
        static readonly int MemoryAddress_KeyExit = 0x7E1435;
        static readonly int adjMemoryAddress_KeyExit = 0xF50000 + (MemoryAddress_KeyExit - 0x7E0000);
        static readonly string AdjustedMemoryAddress_KeyExit = adjMemoryAddress_KeyExit.ToString("X");

        public string MemoryAddressValue_OtherExits;
        static readonly int MemoryAddress_OtherExits = 0x7E1493;
        static readonly int adjMemoryAddress_OtherExits = 0xF50000 + (MemoryAddress_OtherExits - 0x7E0000);
        static readonly string AdjustedMemoryAddress_OtherExits = adjMemoryAddress_OtherExits.ToString("X");

        public string MemoryAddressValue_LevelCounter;
        static readonly int MemoryAddress_LevelCounter = 0x7E1F2E;
        static readonly int adjMemoryAddress_LevelCounter = 0xF50000 + (MemoryAddress_LevelCounter - 0x7E0000);
        static readonly string AdjustedMemoryAddress_LevelCounter = adjMemoryAddress_LevelCounter.ToString("X");

        public string MemoryAddressValue_InGame;
        static readonly int MemoryAddress_InGame = 0x7E1F15;
        static readonly int adjMemoryAddress_InGame = 0xF50000 + (MemoryAddress_InGame - 0x7E0000);
        static readonly string AdjustedMemoryAddress_InGame = adjMemoryAddress_InGame.ToString("X");

        //public string MemoryAddressValue_GreenSwitchActivated;
        //static readonly int MemoryAddress_GreenSwitchActivated = 0x7E1F27;
        //static readonly int adjMemoryAddress_GreenSwitchActivated = 0xF50000 + (MemoryAddress_GreenSwitchActivated - 0x7E0000);
        //static readonly string AdjustedMemoryAddress_GreenSwitchActivated = adjMemoryAddress_GreenSwitchActivated.ToString("X");

        //public string MemoryAddressValue_YellowSwitchActivated;
        //static readonly int MemoryAddress_YellowSwitchActivated = 0x7E1F28;
        //static readonly int adjMemoryAddress_YellowSwitchActivated = 0xF50000 + (MemoryAddress_YellowSwitchActivated - 0x7E0000);
        //static readonly string AdjustedMemoryAddress_YellowSwitchActivated = adjMemoryAddress_YellowSwitchActivated.ToString("X");

        //public string MemoryAddressValue_BlueSwitchActivated;
        //static readonly int MemoryAddress_BlueSwitchActivated = 0x7E1F29;
        //static readonly int adjMemoryAddress_BlueSwitchActivated = 0xF50000 + (MemoryAddress_BlueSwitchActivated - 0x7E0000);
        //static readonly string AdjustedMemoryAddress_BlueSwitchActivated = adjMemoryAddress_BlueSwitchActivated.ToString("X");

        //public string MemoryAddressValue_RedSwitchActivated;
        //static readonly int MemoryAddress_RedSwitchActivated = 0x7E1F2A;
        //static readonly int adjMemoryAddress_RedSwitchActivated = 0xF50000 + (MemoryAddress_RedSwitchActivated - 0x7E0000);
        //static readonly string AdjustedMemoryAddress_RedSwitchActivated = adjMemoryAddress_RedSwitchActivated.ToString("X");

        //var Total = Number(levelCounter + GreenSwitchActivated + YellowSwitchActivated + BlueSwitchActivated + RedSwitchActivated);

        public MainWindow()
        {
            InitializeComponent();
            CurrentBackgroundColor = (SolidColorBrush)GridMain.Background;
            CurrentTextColor = (SolidColorBrush)Label_LevelDeathCount.Foreground;
            InitializeWebSocket();
        }

        private void InitializeWebSocket()
        {
            ws = new WebSocket("ws://localhost:8080");

            ws.OnOpen += (sender, e) =>
            {
                //MessageBox.Show("WebSocket connected");

                var deviceListRequest = new
                {
                    Opcode = "DeviceList",
                    Space = "SNES"
                };
                ws.Send(JsonConvert.SerializeObject(deviceListRequest));

                var attachRequest = new
                {
                    Opcode = "Attach",
                    Space = "SNES",
                    Operands = new[] { "SD2SNES COM3" }
                };
                ws.Send(JsonConvert.SerializeObject(attachRequest));

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(16); // 16ms is approximately 60fps (checking each address once every 3 frames)
                timer.Tick += Timer_Tick;
                timer.Start();

            };

            ws.OnMessage += (sender, e) =>
            {
                ProcessMemoryAddressResponse_DeathCheck(e.RawData);
                ProcessMemoryAddressResponse_KeyExit(e.RawData);
                ProcessMemoryAddressResponse_OtherExits(e.RawData);
                ProcessMemoryAddressResponse_LevelCounter(e.RawData);
                ProcessMemoryAddressResponse_InGame(e.RawData);
            };

            ws.OnError += (sender, e) =>
                {
                    //MessageBox.Show("WebSocket error: " + e.Message);
                };

            ws.OnClose += (sender, e) =>
            {
                if (e.Code == (ushort)CloseStatusCode.Normal)
                {
                    //MessageBox.Show("WebSocket closed normally.");
                }
                else
                {
                    //MessageBox.Show($"WebSocket closed with code {e.Code}: {e.Reason}");
                }
            };

            try
            {
                ws.Connect();
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebSocket connection failed: " + ex.Message);
                this.Close();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                SendGetAddressRequest(ws, AdjustedMemoryAddress_DeathCheck, AdjustedMemoryAddress_KeyExit, AdjustedMemoryAddress_OtherExits, AdjustedMemoryAddress_LevelCounter, AdjustedMemoryAddress_InGame); //AdjustedMemoryAddressValue_GreenSwitchActivated, AdjustedMemoryAddressValue_YellowSwitchActivated. AdjustedMemoryAddressValue_BlueSwitchActivated, AdjustedMemoryAddressValue_RedSwitchActivated
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebSocket connection failed: " + ex.Message);
                this.Close();
            }
        }

        private static void SendGetAddressRequest(WebSocket ws, string MemoryAddressValue_DeathCheck, string MemoryAddressValue_KeyExit, string MemoryAddressValue_OtherExits, string MemoryAddressValue_LevelCounter, string MemoryAddressValue_InGame)
        {
            var getAddressRequest = new
            {
                Opcode = "GetAddress",
                Space = "SNES",
                Operands = new[] { MemoryAddressValue_DeathCheck, "1", 
                    MemoryAddressValue_KeyExit, "1", 
                    MemoryAddressValue_OtherExits, "1" , 
                    MemoryAddressValue_LevelCounter , "1",
                    MemoryAddressValue_InGame, "1"
                }
            };
            ws.Send(JsonConvert.SerializeObject(getAddressRequest));
        }

            private void ProcessMemoryAddressResponse_DeathCheck(byte[] rawData)
            {
                string MemoryAddressValue_DeathCheck = BitConverter.ToString(rawData).Substring(BitConverter.ToString(rawData).Length - 14, 2);
                if ((MemoryAddressValue_DeathCheck != "09") && (DeathState == true))
                {
                    DeathState = false;
                }

                if ((MemoryAddressValue_DeathCheck == "09") && (DeathState == false))
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        LevelDeathCount = Int32.Parse(TextBlock_LevelDeathCount.Text);
                        LevelDeathCount++;
                        TextBlock_LevelDeathCount.Text = LevelDeathCount.ToString();

                        TotalDeathCount = Int32.Parse(TextBlock_TotalDeathCount.Text);
                        TotalDeathCount++;
                        TextBlock_TotalDeathCount.Text = TotalDeathCount.ToString();
                        CounterRange();
                    });
                DeathState = true;
                }
            }

        private void ProcessMemoryAddressResponse_KeyExit(byte[] rawData)
        {
            string MemoryAddressValue_KeyExit = BitConverter.ToString(rawData).Substring(BitConverter.ToString(rawData).Length - 11, 2);
            if (MemoryAddressValue_KeyExit != "00")
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    LevelDeathCount = Int32.Parse(TextBlock_LevelDeathCount.Text);
                    LevelDeathCount = 0;
                    TextBlock_LevelDeathCount.Text = LevelDeathCount.ToString();
                    CounterRange();
                });
            }
        }

        private void ProcessMemoryAddressResponse_OtherExits(byte[] rawData)
        {
            string MemoryAddressValue_OtherExits = BitConverter.ToString(rawData).Substring(BitConverter.ToString(rawData).Length - 8, 2);
            if (MemoryAddressValue_OtherExits != "00")
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    LevelDeathCount = Int32.Parse(TextBlock_LevelDeathCount.Text);
                    LevelDeathCount = 0;
                    TextBlock_LevelDeathCount.Text = LevelDeathCount.ToString();
                    CounterRange();
                });
            }
        }

        private void ProcessMemoryAddressResponse_LevelCounter(byte[] rawData)
        {
            string MemoryAddressValue_LevelCounter = BitConverter.ToString(rawData).Substring(BitConverter.ToString(rawData).Length - 5, 2);
            int LevelCountCurrent = Convert.ToInt32(MemoryAddressValue_LevelCounter, 16);

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                TextBlock_LevelCountCurrent.Text = LevelCountCurrent.ToString();
            });
        }

        private void ProcessMemoryAddressResponse_InGame(byte[] rawData)
        {
            string MemoryAddressValue_InGame = BitConverter.ToString(rawData).Substring(BitConverter.ToString(rawData).Length - 2);
            if (MemoryAddressValue_InGame != "02")
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    TextBlock_LevelCountCurrent.Text = "??";
                });
            }
        }

        private void CounterRange()
        {
            if (Int32.Parse(TextBlock_LevelDeathCount.Text)>= 999999)
            {
                TextBlock_LevelDeathCount.Text = "999999";
            }
            else if (Int32.Parse(TextBlock_LevelDeathCount.Text) <= 0)
            {
                TextBlock_LevelDeathCount.Text = "0";
            }

            if (Int32.Parse(TextBlock_TotalDeathCount.Text) >= 999999)
            {
                TextBlock_TotalDeathCount.Text = "999999";
            }
            else if (Int32.Parse(TextBlock_TotalDeathCount.Text) <= 0)
            {
                TextBlock_TotalDeathCount.Text = "0";
            }
        }

        private void MenuItem_Click_Settings(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new(this);
            settingsWindow.ShowDialog();
            if (settingsWindow.SettingsOK)
            {
                GridMain.Background = settingsWindow.ChangeBackgroundColor;
                Label_LevelDeathCount.Foreground = settingsWindow.ChangeTextColor;
                Label_TotalDeathCount.Foreground = settingsWindow.ChangeTextColor;
                TextBlock_LevelDeathCount.Foreground = settingsWindow.ChangeTextColor;
                TextBlock_TotalDeathCount.Foreground = settingsWindow.ChangeTextColor;
                CurrentBackgroundColor = (SolidColorBrush)settingsWindow.NewBackgroundColor;
                CurrentTextColor = (SolidColorBrush)settingsWindow.NewTextColor;
            }
        }

        private void MenuItem_Click_Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_ResetLevel_Click(object sender, RoutedEventArgs e)
        {
            TextBlock_LevelDeathCount.Text = "0";
        }

        private void Button_ResetTotal_Click(object sender, RoutedEventArgs e)
        {
            TextBlock_TotalDeathCount.Text = "0";
        }

        private void TextBox_AddLevelDeaths_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!int.TryParse(e.Text, out _))
            {
                e.Handled = true;
            }
        }

        private void TextBox_AddTotalDeaths_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!int.TryParse(e.Text, out _))
            {
                e.Handled = true;
            }
        }

        private void Button_AddLevelDeaths_Click(object sender, RoutedEventArgs e)
        {
            LevelDeathCount = Int32.Parse(TextBlock_LevelDeathCount.Text);
            LevelDeathCount += Int32.Parse(TextBox_AddLevelDeaths.Text);
            TextBlock_LevelDeathCount.Text = LevelDeathCount.ToString();
            CounterRange();
        }

        private void Button_AddTotalDeaths_Click(object sender, RoutedEventArgs e)
        {
            TotalDeathCount = Int32.Parse(TextBlock_TotalDeathCount.Text);
            TotalDeathCount += Int32.Parse(TextBox_AddTotalDeaths.Text);
            TextBlock_TotalDeathCount.Text = TotalDeathCount.ToString();
            CounterRange();
        }

        private void ButtonLevelPlus_Click(object sender, RoutedEventArgs e)
        {
            LevelDeathCount = Int32.Parse(TextBlock_LevelDeathCount.Text);
            LevelDeathCount++;
            TextBlock_LevelDeathCount.Text = LevelDeathCount.ToString();
            CounterRange();
        }

        private void ButtonLevelMinus_Click(object sender, RoutedEventArgs e)
        {
            LevelDeathCount = Int32.Parse(TextBlock_LevelDeathCount.Text);
            LevelDeathCount--;
            TextBlock_LevelDeathCount.Text = LevelDeathCount.ToString();
            CounterRange();
        }

        private void ButtonTotalPlus_Click(object sender, RoutedEventArgs e)
        {
            TotalDeathCount = Int32.Parse(TextBlock_TotalDeathCount.Text);
            TotalDeathCount++;
            TextBlock_TotalDeathCount.Text = TotalDeathCount.ToString();
            CounterRange();
        }

        private void ButtonTotalMinus_Click(object sender, RoutedEventArgs e)
        {
            TotalDeathCount = Int32.Parse(TextBlock_TotalDeathCount.Text);
            TotalDeathCount--;
            TextBlock_TotalDeathCount.Text = TotalDeathCount.ToString();
            CounterRange();
        }

        private async void Button_GetExitCount_Click(object sender, RoutedEventArgs e)
        {
            string hackName = TextBox_HackName.Text;
            string lengthText = await SMWCentralAPICall(hackName);
            TextBlock_LevelCountTotal.Text = lengthText;
        }

        static async Task<string> SMWCentralAPICall(string hackName)
        {
            string apiUrl = $"https://www.smwcentral.net/ajax.php?a=getsectionlist&s=smwhacks&f[name]={hackName}";
            string lengthText = "??";  // Default value in case there's no match

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
                            if (name == hackName)
                            {
                                string length = item["fields"]["length"].ToString();
                                lengthText = length.Replace(" exit(s)", string.Empty).Trim();
                                break;
                            }
                        }
                    }
                }
            }
            return lengthText;
        }
    }
}