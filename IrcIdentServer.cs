using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Irc
{
    public static class IrcIdentServer
    {
        static private readonly int port = 113;
        static public string UserName;
        static public int Timeout;
        static private ManualResetEvent CanStart = new ManualResetEvent(false);
        static private bool doOnce;
        static public void Start(string username, int timeout)
        {
            Timeout = timeout;
            UserName = username;
            if (doOnce)
            {
                CanStart.WaitOne();
            }
            else
            {
                doOnce = true;
            }
            new Thread(new ThreadStart(Server)).Start();
            Console.WriteLine("Ident server started");
        }
        static private void Server()
        {
            CanStart.Reset();
            try
            {
                Thread.CurrentThread.Name = "Ident server";
                int timeout = 0;
                TcpListener t = new TcpListener(IPAddress.Any, port);
                t.Start(1);
                while (timeout < Timeout * 4)
                {
                    if (t.Pending())
                    {
                        timeout = Timeout * 4;
                        TcpClient tc = t.AcceptTcpClient();
                        NetworkStream ns = tc.GetStream();
                        StreamReader sr = new StreamReader(ns);
                        ns.ReadTimeout = Timeout * 1000;
                        string s = sr.ReadLine();
                        Console.WriteLine("Ident request : " + s);
                        string[] ss = s.Split(',');
                        StreamWriter sw = new StreamWriter(ns);
                        sw.WriteLine("{0}, {1} : USERID : UNIX : {2}", ss[1].Substring(1), ss[0], UserName);
                        Console.WriteLine("Ident reply : {0}, {1} : USERID : UNIX : {2}", ss[1].Substring(1), ss[0], UserName);
                        sw.Flush();
                        sw.Close();
                        ns.Close();
                        tc.Close();
                    }
                    Thread.Sleep(250);
                    timeout++;
                }
            }
            finally
            {
                CanStart.Set();
            }
        }
    }
}
