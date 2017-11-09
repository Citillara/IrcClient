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
        static private string m_Username;
        static private int m_Timeout;
        static private ManualResetEvent m_CanStart = new ManualResetEvent(false);
        static private bool m_doOnce;
        static public void Start(string username, int timeout)
        {
            m_Timeout = timeout;
            m_Username = username;
            if (m_doOnce)
            {
                m_CanStart.WaitOne();
            }
            else
            {
                m_doOnce = true;
            }
            new Thread(new ThreadStart(Server)).Start();
            Console.WriteLine("Ident server started");
        }
        static private void Server()
        {
            m_CanStart.Reset();
            try
            {
                Thread.CurrentThread.Name = "Ident server";
                int timeout = 0;
                TcpListener t = new TcpListener(IPAddress.Any, port);
                t.Start(1);
                while (timeout < m_Timeout * 4)
                {
                    if (t.Pending())
                    {
                        timeout = m_Timeout * 4;
                        TcpClient tc = t.AcceptTcpClient();
                        NetworkStream ns = tc.GetStream();
                        StreamReader sr = new StreamReader(ns);
                        ns.ReadTimeout = m_Timeout * 1000;
                        string s = sr.ReadLine();
                        Console.WriteLine("Ident request : " + s);
                        string[] ss = s.Split(',');
                        StreamWriter sw = new StreamWriter(ns);
                        sw.WriteLine("{0}, {1} : USERID : UNIX : {2}", ss[1].Substring(1), ss[0], m_Username);
                        Console.WriteLine("Ident reply : {0}, {1} : USERID : UNIX : {2}", ss[1].Substring(1), ss[0], m_Username);
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
                m_CanStart.Set();
            }
        }
    }
}
