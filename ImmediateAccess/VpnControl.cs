﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Data.Common;

namespace ImmediateAccess
{
    class VpnControl
    {
        public static RasDialProcess RasDialProcess = null;
        private static string RasDialExe = "rasdial.exe";
        private static string VpnProfileString = "Below Average AD - VPN";
        public static async Task Connect()
        {
            Logger.Info("RasDial: Preparing to connect...", ConsoleColor.DarkCyan);
            await RasDial("\"" + VpnProfileString + "\"");
        }
        public static async Task Disconnect()
        {
            Logger.Info("RasDial: Disconnecting from " + VpnProfileString + "...", ConsoleColor.DarkCyan);
            await RasDial("\"" + VpnProfileString + "\" /disconnect");
        }
        private static Task<bool> RasDial(string Arguments)
        {
            bool result = false;
            return Task.Run(() => {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = RasDialExe;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.Arguments = Arguments;
                RasDialProcess = new RasDialProcess();
                RasDialProcess.StartInfo = startInfo;
                RasDialProcess.EnableRaisingEvents = true;
                RasDialProcess.OutputDataReceived += Process_OutputDataReceived;
                RasDialProcess.Start();
                RasDialProcess.BeginOutputReadLine();
                RasDialProcess.WaitForExit(10000);
                if (!RasDialProcess.HasExited)
                {
                    RasDialProcess.Kill();
                }
                else if(RasDialProcess.SuccessFromRasDial)
                {
                    result = true;
                }
                if (result)
                {
                    Logger.Info("RasDial: Success.", ConsoleColor.Green);
                }
                else
                {
                    Logger.Error("RasDial: Failure.");
                }
                RasDialProcess.Dispose();
                RasDialProcess = null;
                return result;
            });
        }
        private static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            RasDialProcess process = (RasDialProcess)sender;
            if (e.Data == null) return;
            if (e.Data == "Command completed successfully.")
            {
                process.SuccessFromRasDial = true;
                return;
            }
            Logger.Info("RasDial: " + e.Data, ConsoleColor.DarkCyan);
        }
    }
    class RasDialProcess : Process
    {
        public bool SuccessFromRasDial = false;
    }
}
