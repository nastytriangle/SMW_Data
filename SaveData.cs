using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SMW_Data
{
    public class SaveData
    {
        public string LevelTime { get; set; } = "0.00";
        public string LastLevelTime { get; set; } = "0.00";
        public string TotalTime { get; set; } = "0.00";
        public string HackName { get; set; } = "HackName";

        public string Creator { get; set; } = "Author";
        public int LevelDeaths { get; set; } = 0;
        public int TotalDeaths { get; set; } = 0;
        public SolidColorBrush BackgroundColor { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF463F3F");
        public SolidColorBrush TextColor { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFFFFF");
        public int LevelTimerAccuracy { get; set; } = 1;
        public int TotalTimerAccuracy { get; set; } = 1;
        public string ExitCountTotal { get; set; } = "??";
        public int DeathImage { get; set; } = 2;
        public FontFamily TitleFont { get; set; } = new FontFamily("Segoe UI");
        public FontFamily AuthorFont { get; set; } = new FontFamily("Segoe UI");
        public int PreviousExitCounter { get; set; }
        public bool TrackDeaths { get; set; }
        public bool TrackExits { get; set; }
        public bool TrackInGame { get; set; }
        public bool TrackSwitches { get; set; }
    }
}
