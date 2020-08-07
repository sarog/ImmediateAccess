﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Threading.Tasks;

namespace ImmediateAccess
{
    public static class nConsole
    {
        private static TcpListener server;
        private static List<nConsoleConnection> clients = new List<nConsoleConnection>();
        private static string buffer = "";
        private static Queue<string> logs = new Queue<string>();
        private static ConsoleColor cc = ConsoleColor.White;
        private static Timer tcpKeepAlive;
        private static int logLineLimit = 10000;
        private static int tcpKeepAliveIntervalMS = 5000;
        private static int tcpPort = 7362;
        /// <summary>
        /// This method sets up the net console.
        /// </summary>
        public static void Setup()
        {
            Logger.Info("nConsole: Creating net console server on port " + tcpPort + "...");
            try
            {
                tcpKeepAlive = new Timer(tcpKeepAliveIntervalMS);
                tcpKeepAlive.Elapsed += TcpKeepAlive_Elapsed;
                tcpKeepAlive.Start();
                server = new TcpListener(IPAddress.Parse("127.0.0.1"), tcpPort);
                server.Start();
                _ = ConnectLoop();
            }
            catch (IOException e)
            {
                Logger.Warning("nConsole: Failed to create net console server: " + e.Message);
            }
        }
        /// <summary>
        /// This method will write text to the Pipe stream.
        /// </summary>
        /// <param name="Text">String: Text to send.</param>
        public static void Write(string Text)
        {
            buffer = buffer + Text;
        }
        /// <summary>
        /// This method will send a line of text to the Pipe client.
        /// </summary>
        /// <param name="Text">String: The line of text to send.</param>
        public static void WriteLine(string Text)
        {
            logs.Enqueue(buffer + Text);
            if (logs.Count > logLineLimit)
            {
                logs.Dequeue();
            }
            SendAll(buffer + Text + "\r\n");
            buffer = "";
        }
        /// <summary>
        /// This method will set the color back to white. (Used for compatibility with Console methods).
        /// </summary>
        public static void ResetColor()
        {
            ForegroundColor = ConsoleColor.White;
        }
        /// <summary>
        /// This getter / setter will send the color that should be set to the Pipe client.
        /// </summary>
        public static ConsoleColor ForegroundColor
        {
            get
            {
                return cc;
            }
            set
            {
                cc = value;
                Write("<#" + value.ToString() + "#>");
            }
        }
        private static void TcpKeepAlive_Elapsed(object sender, ElapsedEventArgs e)
        {
            SendAll("\0");
        }
        private static void SendAll(string Text)
        {
            foreach (nConsoleConnection client in clients.ToArray())
            {
                client.SendText(Text);
            }
        }
        private static void SendQueue(this nConsoleConnection client)
        {
            foreach(string log in logs.ToArray())
            {
                client.SendText(log + "\r\n");
            }
        }
        private static void SendText(this nConsoleConnection client, string Text)
        {
            try
            {
                client.StreamWriter.Write(Text);
                client.StreamWriter.Flush();
            }
            catch (IOException)
            {
                clients.Remove(client);
                string conName = client.ConnectionName;
                client.Dispose();
                Task.Run(() => {
                    Logger.Info("nConsole: Client at " + conName + " lost connection.");
                });
            }
        }
        private static Task ConnectLoop()
        {
            return Task.Run(() => {
                while (true)
                {
                    TcpClient tcpClient = server.AcceptTcpClient();
                    StreamWriter sw = new StreamWriter(tcpClient.GetStream());
                    nConsoleConnection client = new nConsoleConnection()
                    {
                        ConnectionName = tcpClient.Client.RemoteEndPoint.ToString(),
                        TcpClient = tcpClient,
                        StreamWriter = sw
                    };
                    Logger.Info("nConsole: Client connected at " + client.ConnectionName + ".");
                    client.SendQueue();
                    clients.Add(client);
                }
            });
        }
    }
    class nConsoleConnection
    {
        public string ConnectionName;
        public TcpClient TcpClient;
        public StreamWriter StreamWriter;
        public void Dispose()
        {
            StreamWriter.Dispose();
            TcpClient.Dispose();
            ConnectionName = null;
        }
    }
}