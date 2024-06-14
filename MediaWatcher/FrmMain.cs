using System;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Threading;

// How to add your program to the system tray: http://progtuts.info/14/your-c-program-in-the-system-tray/

namespace MediaWatcher
{
    public partial class FrmMain : Form
    {
        RegistryKey MediaWatcherKey = Registry.CurrentUser.CreateSubKey("SOFTWARE\\MediaWatcher");
        DirectoryInfo DirInfo;
        FileInfo FileData;

        string MediaFilePath;
        string[] VideoExt = { "MPG", "MPEG", "MP4", "AVI", "ASF", "MKV", "MOV", "WMV", "FLV", "M4V", "RAM", "DIVX", "SRT" };
        string DspHours;
        string DspMins;
        string DspSecs;

        int Hours = 0;
        int Mins = 0;
        int Secs = 0;

        private Thread Thread_ScanMediaFilePaths;
        private Thread Thread_Timer;

        public delegate void Thread_ScanMediaFilePaths_Delegate_1();
        public delegate void Thread_ScanMediaFilePaths_Delegate_2();
        public delegate void Thread_Timer_Delegate_1();
        public delegate void Thread_Timer_Delegate_2();

        public FrmMain()
        {
            InitializeComponent();
        }

        #region "Form - Events"

        private void FrmMain_Load(object sender, EventArgs e)
        {
            ConfigureDGV();
            GetMediaFolders();

            Thread_Timer = new Thread(new ThreadStart(this.Timer));
            Thread_Timer.Start();

            this.WindowState = FormWindowState.Minimized;
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            KillThreads();
        }

        private void FrmMain_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        #endregion

        private void ConfigureDGV()
        {
            Double DGVWidth;
            DataGridViewColumn Column;

            DGVWidth = DGVMediaFolders.Width;

            DGVMediaFolders.Rows.Clear();
            DGVMediaFolders.Columns.Clear();

            DGVMediaFolders.Columns.Add("", "Drive");
            DGVMediaFolders.Columns.Add("", "File Path");

            // Define column widths
            Column = DGVMediaFolders.Columns[0];
            Column.Width = Convert.ToInt16(Math.Round(DGVWidth / 100 * 10, 0)); // Calculate size as % of overall column width
            Column = DGVMediaFolders.Columns[1];
            Column.Width = Convert.ToInt16(Math.Round(DGVWidth / 100 * 90, 0));

            DGVMediaFolders.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        private void GetMediaFolders()
        {
            DGVMediaFolders.Rows.Clear();

            foreach (string KeyName in MediaWatcherKey.GetValueNames())
            {
                if (Regex.IsMatch(KeyName, "^FilePath[=]"))
                {
                    DirInfo = new DirectoryInfo(MediaWatcherKey.GetValue(KeyName).ToString());
                    DGVMediaFolders.Rows.Add(DirInfo.Root.ToString(), MediaWatcherKey.GetValue(KeyName));
                }
            }
        }

        private Int64 GetHighestRegValue()
        {
            Int64 HighValue = 0;

            foreach (string KeyName in MediaWatcherKey.GetValueNames())
            {
                if (Regex.IsMatch(KeyName, "^FilePath[=]"))
                {
                    string[] ValueArray = KeyName.Split('=');

                    if (HighValue == null)
                    {
                        HighValue = Convert.ToInt64(ValueArray[1]);
                    }
                    else if (Convert.ToInt64(ValueArray[1]) > HighValue)
                    {
                        HighValue = Convert.ToInt64(ValueArray[1]);
                    }
                }
            }

            HighValue++;

            return HighValue;
        }

        private bool SeeIfValueExists(string FilePath)
        {
            foreach (string KeyName in MediaWatcherKey.GetValueNames())
            {
                if (Regex.IsMatch(KeyName, "^FilePath[=]"))
                {
                    if (FilePath.ToLower() == MediaWatcherKey.GetValue(KeyName).ToString().ToLower())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void Start_ScanMediaFilePaths()
        {
            // Reset counter
            Hours = 0;
            Mins = 0;
            Secs = 0;

            if (DGVMediaFolders.Rows.Count != 0)
            {
                Thread_ScanMediaFilePaths = new Thread(new ThreadStart(this.ScanMediaFilePaths));
                Thread_ScanMediaFilePaths.Start();
            }
            else
            {
                MessageBox.Show("Information - A Media File Path Has Not Been Specified \n\n" +
                "Please provide Media Watcher with a local or network file path that contains media files by using the 'Add' button.", "Information - A Media File Path Has Not Been Specified", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #region "Form - Controls"

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog CreateFolderDialog = new FolderBrowserDialog();

            CreateFolderDialog.Description = "Browse To A Local Or Network File Path That Contains Video Media";
            CreateFolderDialog.ShowNewFolderButton = false;

            if (CreateFolderDialog.ShowDialog() == DialogResult.OK)
            {
                MediaFilePath = CreateFolderDialog.SelectedPath;

                if (SeeIfValueExists(MediaFilePath) == false)
                {
                    DirInfo = new DirectoryInfo(MediaFilePath);
                    DGVMediaFolders.Rows.Add(DirInfo.Root.ToString(), MediaFilePath);
                    MediaWatcherKey.SetValue("FilePath=" + GetHighestRegValue(), MediaFilePath, RegistryValueKind.String);
                }
                else
                {
                    MessageBox.Show("Information - File path has already been added, please choose another location", "Information - File path has already been added", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (DGVMediaFolders.Rows.Count != 0)
            {
                int SelectedDGVRow = DGVMediaFolders.CurrentCell.RowIndex; 
                string DGVCellValue;

                DGVCellValue = DGVMediaFolders.Rows[SelectedDGVRow].Cells[1].Value.ToString();

                foreach (string KeyName in MediaWatcherKey.GetValueNames())
                {
                    if (Regex.IsMatch(KeyName, "^FilePath[=]"))
                    {
                        if (DGVCellValue.ToLower() == MediaWatcherKey.GetValue(KeyName).ToString().ToLower())
                        {
                            Registry.LocalMachine.OpenSubKey("SOFTWARE\\MediaWatcher", true);

                            try
                            {
                                MediaWatcherKey.DeleteValue(KeyName, true); // Delete the key
                            }
                            catch (Exception Error)
                            {
                            }

                            GetMediaFolders();
                            break;
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Information - There Are No Media File Paths To Remove \n\n" +
                "Please provide Media Watcher with a local or network file path that contains media files by using the 'Add' button.", "Information - There Are No Media File Paths To Remove", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnScan_Click(object sender, EventArgs e)
        {
            Start_ScanMediaFilePaths();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        #endregion

        #region "Thread - ScanMediaFilePaths"

        private void ScanMediaFilePaths()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Thread_ScanMediaFilePaths_Delegate_1(Thread_ScanMediaFilePaths_UpdateForm_1));
            }

            string MediaFilePath; 
            string FileExt; 
            string Name; 
            string EpisodeData; 

            int Season = 0; 

            foreach (string KeyName in MediaWatcherKey.GetValueNames())
            { 
                if (Regex.IsMatch(KeyName, "^FilePath[=]"))
                { 
                    MediaFilePath = MediaWatcherKey.GetValue(KeyName).ToString();

                    try
                    {
                        foreach (string File in Directory.GetFiles(MediaFilePath))
                        { 
                            try
                            {
                                FileData = new FileInfo(File); // Get information about the file
                                FileExt = FileData.Extension.Remove(0, 1); // Turn '.mp4' into 'mp4'

                                for (int count = 0; count < VideoExt.Length; count++)
                                { 
                                    if (FileExt.ToLower() == VideoExt[count].ToLower())
                                    {
                                        bool TVEpisode = false;

                                        if (Regex.IsMatch(FileData.Name.ToLower(), "-*cd[1-2]."))
                                        { 
                                            // If the file matches a multi-part video then:
                                            string[] ArrayValues = FileData.Name.Split('-'); // Turn 'Fred-cd1.avi' into 'Fred.avi'
                                            Name = ArrayValues[0].Trim();
                                        }
                                        else if (Regex.IsMatch(FileData.Name.ToLower(), "-*s[0-9][0-9]e[0-9][0-9].") || Regex.IsMatch(FileData.Name.ToLower(), "-*s[0-9]e[0-9][0-9]."))
                                        {  
                                            // If the file matches a TV episode of type 'Fred - S01E01.avi' OR type 'Fred - S1E01.avi' then:
                                            TVEpisode = true;

                                            string[] ArrayValues = FileData.Name.Split('-');  // Turn 'Fred - S01E01.avi' OR 'Fred - S1E01.avi' into 'Fred'
                                            Name = ArrayValues[0].Trim();
                                            EpisodeData = ArrayValues[1].Remove(ArrayValues[1].Length - FileData.Extension.Length, FileData.Extension.Length).Trim().ToLower(); // Turn 'S01E01.avi' into 'S01E01'
                                            Array.Clear(ArrayValues, 0, ArrayValues.Length);
                                            ArrayValues = EpisodeData.Split('e'); // Turn 'S01E01' into 'S01'
                                            Season = Convert.ToInt16(ArrayValues[0].Remove(0, 1)); // Turn 'S01' into '1'
                                        }
                                        else
                                        {
                                            // If the file matches a single part video then:
                                            // Turn 'Fred.avi' into 'Fred'
                                            Name = FileData.Name.Remove(FileData.Name.Length - FileData.Extension.Length, FileData.Extension.Length).Trim(); 
                                        }

                                        if (TVEpisode == true)
                                        { 
                                            // If the current file is a TV episode then:
                                            if (Directory.Exists(FileData.DirectoryName + "\\" + Name + "\\Season " + Season.ToString()) == false)
                                            {
                                                try
                                                {
                                                    Directory.CreateDirectory(FileData.DirectoryName + "\\" + Name + "\\Season " + Season.ToString());
                                                }
                                                catch (Exception ex)
                                                {
                                                }
                                            }

                                            try
                                            {
                                                System.IO.File.Move(File, FileData.DirectoryName + "\\" + Name + "\\Season " + Season.ToString() + "\\" + FileData.Name);
                                            }
                                            catch (Exception ex)
                                            {
                                            }
                                        }
                                        else if (TVEpisode == false)
                                        { 
                                            // If the current file is a movie then:
                                            if (Directory.Exists(FileData.DirectoryName + "\\" + Name) == false)
                                            {
                                                try
                                                {
                                                    Directory.CreateDirectory(FileData.DirectoryName + "\\" + Name);
                                                }
                                                catch (Exception ex)
                                                {
                                                }
                                            }

                                            try
                                            {
                                                System.IO.File.Move(File, FileData.DirectoryName + "\\" + Name + "\\" + FileData.Name);
                                            }
                                            catch (Exception ex)
                                            {
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            { 
                            }
                        }
                    }
                    catch (Exception ex)
                    { 
                    }
                }
            }

            if (this.InvokeRequired)
            {
                this.Invoke(new Thread_ScanMediaFilePaths_Delegate_2(Thread_ScanMediaFilePaths_UpdateForm_2));
            }
        }

        private void Thread_ScanMediaFilePaths_UpdateForm_1()
        {
            BtnScan.Enabled = false;
            BtnDelete.Enabled = false;
            BtnAdd.Enabled = false;

            LblStatus.Text = "Status: Searching Media File Paths";
            LblStatus.Update();
        }

        private void Thread_ScanMediaFilePaths_UpdateForm_2()
        {
            BtnScan.Enabled = true;
            BtnDelete.Enabled = true;
            BtnAdd.Enabled = true;

            LblStatus.Text = "Status: Idle";
            LblStatus.Update();
        }

        #endregion

        #region "Thread - Timer"

        private void Timer()
        {
            while (true)
            {

                if (Hours < 10)
                {
                    DspHours = "0" + Hours.ToString();
                }
                else
                {
                    DspHours = Hours.ToString();
                }

                if (Mins < 10)
                {
                    DspMins = "0" + Mins.ToString();
                }
                else
                {
                    DspMins = Mins.ToString();
                }

                if (Secs < 10)
                {
                    DspSecs = "0" + Secs.ToString();
                }
                else
                {
                    DspSecs = Secs.ToString();
                }

                if (this.InvokeRequired)
                {
                    this.Invoke(new Thread_Timer_Delegate_1(Thread_Timer_UpdateForm));
                }

                if ((DspHours + ":" + DspMins + ":" + DspSecs) == "00:05:00")
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Thread_Timer_Delegate_2(Thread_Timer_StartScan));
                    }
                }

                Thread.Sleep(1000);

                Secs++;

                if (Secs >= 60)
                {
                    Secs = 0;
                    Mins++;
                }

                if (Mins >= 60)
                {
                    Mins = 0;
                    Hours++;
                }

                if (Hours >= 24)
                {
                    Hours = 0;
                }
            }
        }

        private void Thread_Timer_UpdateForm()
        {
            LblTimer.Text = "Timer: " + DspHours + ":" + DspMins + ":" + DspSecs;
            LblTimer.Update();
        }

        private void Thread_Timer_StartScan()
        {
            Start_ScanMediaFilePaths();
        }

        #endregion

        #region "Kill Running Threads"

        private void KillThreads()
        {
            try
            {
                if (Thread_ScanMediaFilePaths.IsAlive == true)
                {
                    try
                    {
                        Thread_ScanMediaFilePaths.Abort();
                    }
                    catch (Exception Error)
                    {
                    }
                }
            }
            catch (Exception Error)
            {
            }

            try
            {
                if (Thread_Timer.IsAlive == true)
                {
                    try
                    {
                        Thread_Timer.Abort();
                    }
                    catch (Exception Error)
                    {
                    }
                }
            }
            catch (Exception Error)
            {
            }
        }

        #endregion
    }
}