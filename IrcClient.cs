using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;


namespace Irc
{
    public class IrcClient
    {
        // Constants
        private static readonly char[] USERHOST_SIGNS = { '!', '@' };
        private static readonly char[] CTCP_SIGN = { (char)0x01, ' ' };
        private static readonly byte CR = 0x0D;
        private static readonly byte LF = 0x0A;
        private static readonly int BUFFER_SIZE = 16384;
        private static readonly int TIME_WAIT = 200; // Threads waiting time when no work is due
        public static readonly string Version = "3";
        
        // Events
        public delegate void IrcClientOnPrivateMessageEventHandler(IrcClient sender, IrcClientOnPrivateMessageEventArgs args);
        public event IrcClientOnPrivateMessageEventHandler OnPrivateMessage;
        public delegate void IrcClientPerformEventHandler(IrcClient sender);
        public event IrcClientPerformEventHandler OnPerform;

        public delegate void IrcClientOnLogEventHandler(IrcClient sender, IrcClientOnLogEventArgs args);
        public event IrcClientOnLogEventHandler OnLog;

        public delegate void IrcClientOnNoticeEventHandler(IrcClient sender, IrcMessage args);
        public event IrcClientOnNoticeEventHandler OnNotice;

        public delegate void IrcClientOnJoinEventHandler(IrcClient sender, IrcClientOnJoinEventArgs args);
        public event IrcClientOnJoinEventHandler OnJoin;

        public delegate void IrcClientOnPartEventHandler(IrcClient sender, IrcClientOnPartEventArgs args);
        public event IrcClientOnPartEventHandler OnPart;

        public delegate void IrcClientOnQuitEventHandler(IrcClient sender, IrcClientOnQuitEventArgs args);
        public event IrcClientOnQuitEventHandler OnQuit;

        public delegate void IrcClientOnModeEventHandler(IrcClient sender, IrcClientOnModeEventArgs args);
        public event IrcClientOnModeEventHandler OnMode;

        public delegate void IrcClientOnChannelNickListReceivedEventHandler(IrcClient sender, IrcClientOnChannelNickListReceivedEventArgs args);
        public event IrcClientOnChannelNickListReceivedEventHandler OnChannelNickListRecived;

        public delegate void IrcClientOnUnknownCommandEventHandler(IrcClient sender, IrcMessage message);
        public event IrcClientOnUnknownCommandEventHandler OnUnknownCommand;

        public delegate void IrcClientOnDisconnectEventHandler(IrcClient sender, bool wasManualDisconnect);
        public event IrcClientOnDisconnectEventHandler OnDisconnect;

        // Network manager
        private TcpClient m_Client;
        private string m_Host;
        private int m_Port;
        private readonly IPAddress m_IPAddress;
        private NetworkStream m_NetworkStream;
        private StreamWriter m_Writer;
        private StreamReader myReader;
        private Thread myListenerThread;
        private Thread myMessageManagerThread;
        private DateTime myStartTime;
        private bool usingIP = false;
        private ManualResetEvent idleListener;

        public string Password { get; set; }
        private const string VersionString = "Citillara IRC Client library 201711080056 - Check Github for support";

        private bool manualDisconnect = false;
        private bool hasBeenDisconnected = false;

        // Public get only variables
        private bool Connected;
        public bool IsConnected { get { return Connected; } }
        public DateTime StartTime { get { return myStartTime; } }

        // Settings
        private string myNickname;
        public MessageLevel LogLevel { get; set; }

        // Constructor
        public IrcClient(string host, int port, string nick)
        {
            myNickname = nick;
            m_Host = host;
            m_Port = port;
        }
        public IrcClient(IPAddress address, int port, string nick)
        {
            m_IPAddress = address;
            m_Port = port;
            myNickname = nick;
            usingIP = true;
        }
        
        public void Connect()
        {
            if (hasBeenDisconnected)
                throw new ObjectDisposedException("Cannot reconnect after a disconnection. Create a new instance of the class");
            try
            {
                m_Client = new TcpClient();
                idleListener = new ManualResetEvent(false);
                if (usingIP)
                {
                    m_Client.Connect(m_IPAddress, m_Port);
                }
                else
                {
                    m_Client.Connect(m_Host, m_Port);
                }
                Log("IRC client connected", MessageLevel.Info);
                m_NetworkStream = m_Client.GetStream();
                m_NetworkStream.ReadTimeout = 6 * 60 * 1000;
                m_Writer = new StreamWriter(m_NetworkStream);
                myReader = new StreamReader(m_NetworkStream);
                Connected = true;
                if (myMessageManagerThread != null)
                {
                    myMessageManagerThread.Abort();
                }
                if (myListenerThread != null && myListenerThread.IsAlive)
                {
                    myListenerThread.Abort();
                }
                myMessageManagerThread = new Thread(new ThreadStart(MessageManagerLoop));
                myMessageManagerThread.Start();
                myListenerThread = new Thread(new ThreadStart(ListenerLoop));
                myListenerThread.Start();

            }
            catch (Exception e)
            {
                ExceptionState(e);
                Close();
            }
        }

        // Network methods
        private void ListenerLoop()
        {
            Thread.CurrentThread.Name = "Irc Client Listener";
            Encoding encoding = Encoding.GetEncoding(1252);
            myStartTime = DateTime.Now;
            try
            {
                if (string.IsNullOrEmpty(Password))
                {
                    Pass(Password);
                }
                User(myNickname, myNickname, myNickname, myNickname);
                Nick(myNickname);
                int bufferCursor = 0;
                int bufferCursorPrevious = bufferCursor;
                byte[] buffer = new byte[BUFFER_SIZE];  // 16384
                byte[] buff = new byte[1];
                buff[0] = 0;
                bool ok = false;
                while (Connected)
                {
                    do
                    {
                        int read = m_NetworkStream.Read(buffer, bufferCursor, 1);
                        if (read == 0)
                        {
                            break;
                        }
                        ok = (buffer[bufferCursor] == CR) || (buffer[bufferCursor] == LF);
                        if (ok)
                        {
                            ok = false;
                            // Then the message is complete let's give it to the processor
                            if (bufferCursor > 1)
                                AddMessageToManager(encoding.GetString(buffer, 0, bufferCursor));
                            bufferCursor = 0;
                        }
                        else
                        {
                            // It's not let's continue
                            if (bufferCursor >= BUFFER_SIZE - 1)
                            {
                                // opps we're going for buffer overflow let's discard that
                                Log("Overflow : " + encoding.GetString(buffer), MessageLevel.Warning);
                                bufferCursor = 0;
                            }
                            else
                            {
                                bufferCursor++;
                            }
                        }
                    } while (m_NetworkStream.DataAvailable);

                    // Either not recived everything yet or waiting for stuff later
                    if (bufferCursorPrevious != bufferCursor)
                    {
                        bufferCursorPrevious = bufferCursor;
                    }
                    else
                    {
                        idleListener.WaitOne(TIME_WAIT);
                    }
                }
            }
            catch (OverflowException e)
            {
                System.Diagnostics.Debugger.Break();
                ExceptionState(e);
            }
            catch (Exception e)
            {
                ExceptionState(e);
            }
            finally
            {
                this.Close();
            }
        }

        /// <summary>
        /// Disconnects the client and close all connection. Cannot be reused afterwards
        /// </summary>
        public void Disconnect()
        {
            manualDisconnect = true;
            Close();
        }

        private void Close()
        {
            Connected = false;
            idleListener.Set();
            try
            {
                if (myReader != null) { myReader.Close(); }
                if (myReader != null) { myReader.Dispose(); }
                if (m_Writer != null) { m_Writer.Close(); }
                if (m_Writer != null) { m_Writer.Dispose(); }
                if (m_NetworkStream != null) { m_NetworkStream.Close(); }
                if (m_NetworkStream != null) { m_NetworkStream.Dispose(); }
                if (m_Client != null) { m_Client.Close(); }
            }
            finally
            {
                this.Log("IRC client closing", MessageLevel.Info);
                if (this.OnDisconnect != null && !hasBeenDisconnected)
                {
                    hasBeenDisconnected = true;
                    OnDisconnect(this, manualDisconnect);
                }
                hasBeenDisconnected = true;
            }
        }
        private void ExceptionState(Exception e)
        {
            this.Log(e.ToString(), MessageLevel.Critical);
            this.Close();
        }
        
        public void SendLine(string data)
        {
            if (Connected)
            {
                try
                {
                    Log(data, MessageLevel.Debug);
                    m_Writer.WriteLine(data);
                    m_Writer.Flush();
                }
                catch (Exception e)
                {
                    ExceptionState(e);
                    Close();
                }
            }
        }
        public void SendLine(string format, params object[] arg)
        {
            this.SendLine(string.Format(Thread.CurrentThread.CurrentCulture, format, arg));
        }

        // Message manager
        private Queue<string> myMessageQueue = new Queue<string>();

        string last_message = "";

        private void AddMessageToManager(string message)
        {
            if (message != null)
            {
                lock (myMessageQueue)
                {
                    myMessageQueue.Enqueue(message);
                    if (last_message != message)
                    {
                        last_message = message;
                    }
                }
            }
        }
        private void MessageManagerLoop()
        {

            Thread.CurrentThread.Name = "Irc Client Message Manager";
            int messageQueueCount = 0;
            while (Connected)
            {
                messageQueueCount = myMessageQueue.Count;
                if (messageQueueCount > 0)
                {
                    string message;
                    lock (myMessageQueue)
                    {
                        message = myMessageQueue.Dequeue();
                    }
                    MessageManager(message);
                }
                else
                {
                    idleListener.WaitOne(TIME_WAIT);
                }
            }
        }

        // Misc
        Dictionary<string, List<string>> channelNickList = new Dictionary<string, List<string>>();

        private void MessageManager(string message)
        {
            Log(message, MessageLevel.Debug);
            if (message.StartsWith("PING"))
            {
                string[] splitLine = message.Split(' ');
                SendLine("PONG {0}", splitLine[1]);
            }
            else if (message.StartsWith("ERROR"))
            {
                Quit(message);
            }
            else
            {
                IrcMessage _message = new IrcMessage(message);
                switch (_message.Command)
                {
                    case "005": // Server configuration
                        break;
                    case "333": // Topic who time
                        break;
                    case "353": // List of names
                        RcvListOfNames(_message);
                        break;
                    case "366": // End of list of names
                        RcvEndOfListOfNames(_message);
                        break;
                    case "375": // Start of MOTD
                        break;
                    case "376": // End of MOTD
                        Perform();
                        break;
                    case "433": // Nickname already used
                        break;
                    case "422": // No MOTD
                        Perform();
                        break;
                    case "WHISPER":
                    case "PRIVMSG": // Private message
                        RcvPrivMsg(_message);
                        break;
                    case "JOIN": // Joining channel
                        RcvJoin(_message);
                        break;
                    case "KICK": // Kicked from channel
                        break;
                    case "PART": // Part from channel
                        RcvPart(_message);
                        break;
                    case "NOTICE": // Notice
                        RcvNotice(_message);
                        break;
                    case "MODE": // MODE
                        RcvMode(_message);
                        break;
                    case "CAP": // Capabilities
                        break;
                    default : // Unknown command
                        if (OnUnknownCommand != null)
                        {
                            OnUnknownCommand(this, _message);
                        }
                        break;
                }
            }
        }

        private bool didPerformOnce;
        private void Perform()
        {
            if (!didPerformOnce)
            {
                didPerformOnce = true;
                if (OnPerform != null)
                {
                    OnPerform(this);
                }
            }
        }

        // Recive methods

        private void RcvPrivMsg(IrcMessage message)
        {
            if (message.Parameters[1][0] == 0x01)
            {
                RcvCTCP(message);
            }
            else
            {
                string channel = message.Parameters[0];
                string msg = message.Parameters[1];
                string userid = "", userhost = "";
                string[] split = message.Prefix.Split(USERHOST_SIGNS);

                string nick = split[0];
                if (split.Length > 1)
                    userid = split[1];
                if (split.Length > 2)
                    userhost = split[2];

                bool isforme = (channel.ToLowerInvariant() == myNickname.ToLowerInvariant());


                if (OnPrivateMessage != null)
                {
                    OnPrivateMessage(this, new IrcClientOnPrivateMessageEventArgs(msg, nick, userid, userhost, channel, isforme, message.Tags));
                }
            }
        }
        private void RcvNotice(IrcMessage message)
        {
            if (OnNotice != null)
                OnNotice(this, message);
        }

        private void RcvJoin(IrcMessage message)
        {
            if (OnJoin != null)
            {
                string channel = message.Parameters[0];
                string userid = "", userhost = "";
                string[] split = message.Prefix.Split(USERHOST_SIGNS);

                string nick = split[0];
                if (split.Length > 1)
                    userid = split[1];
                if (split.Length > 2)
                    userhost = split[2];

                bool isforme = (nick.ToLowerInvariant() == myNickname.ToLowerInvariant());

                OnJoin(this, new IrcClientOnJoinEventArgs(nick, userid, userhost, channel, isforme));
            }
        }
        private void RcvPart(IrcMessage message)
        {
            if (OnPart != null)
            {
                string channel = message.Parameters[0];
                string userid = "", userhost = "";
                string[] split = message.Prefix.Split(USERHOST_SIGNS);

                string nick = split[0];
                if (split.Length > 1)
                    userid = split[1];
                if (split.Length > 2)
                    userhost = split[2];

                bool isforme = (nick.ToLowerInvariant() == myNickname.ToLowerInvariant());

                OnPart(this, new IrcClientOnPartEventArgs(nick, userid, userhost, channel, isforme));
            }
        }

        private void RcvCTCP(IrcMessage message)
        {
            string msg = message.Parameters[1];
            string[] split = message.Prefix.Split(USERHOST_SIGNS);
            string sender = split[0];

            string[] s = msg.Split(CTCP_SIGN, StringSplitOptions.RemoveEmptyEntries);
            if (s.Length > 0)
            {
                switch (s[0])
                {
                    case "VERSION":
                        SendLine("NOTICE {0} :{1}VERSION {2}{1}", sender, (char)0x01, VersionString);
                        break;
                    case "PING":
                        if(s.Length > 1)
                            SendLine("NOTICE {0} :{1}PING {2}{1}", sender, (char)0x01, s[1]);
                        break;
                    case "TIME":
                        SendLine("NOTICE {0} :{1}TIME {2}{1}", sender, (char)0x01, DateTime.Now.ToString("ddd MMM d HH:mm:ss yyyy", CultureInfo.CreateSpecificCulture("en-US")));
                        break;
                }
            }
        }

        private void RcvListOfNames(IrcMessage message)
        {

            if (OnChannelNickListRecived != null)
            {
                if (message.Parameters.Length > 3)
                {
                    if (!channelNickList.ContainsKey(message.Parameters[2]))
                    {
                        channelNickList.Add(message.Parameters[2], new List<string>());
                    }
                    channelNickList[message.Parameters[2]]
                        .AddRange(message.Parameters[3].Split(new char[]{' '}, StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }
        private void RcvEndOfListOfNames(IrcMessage message)
        {
            if (OnChannelNickListRecived != null)
            {
                string channel = message.Parameters[1];
                if (channelNickList.ContainsKey(channel))
                {
                    OnChannelNickListRecived(this, new IrcClientOnChannelNickListReceivedEventArgs(channel, channelNickList[channel].ToArray()));
                }
                else
                {
                    OnChannelNickListRecived(this, new IrcClientOnChannelNickListReceivedEventArgs(channel, new string[] {}));
                }
            }
        }

        private void RcvMode(IrcMessage message)
        {
            string channel = message.Parameters[0];
            string userid = "", userhost = "";
            string[] split = message.Prefix.Split(USERHOST_SIGNS);

            string nick = split[0];
            if (split.Length > 1)
                userid = split[1];
            if (split.Length > 2)
                userhost = split[2];

            int number_of_targets = message.Parameters.Length - 2;
            if (number_of_targets == 0)
            {
                string modes = message.Parameters[1];
                List<ModeChange> changes = new List<ModeChange>(modes.Length - 1);
                bool adding = false;
                foreach (char mode in modes)
                {
                    switch (mode)
                    {
                        case '+': adding = true;
                            break;
                        case '-': adding = false;
                            break;
                        default:
                            changes.Add(new ModeChange()
                            {
                                IsAdded = adding,
                                IsGlobalMode = true,
                                Mode = mode,
                                Name = message.Parameters[0]
                            });
                            break;
                    }
                }

                if(this.OnMode != null)
                    OnMode(this, new IrcClientOnModeEventArgs(userid, nick, userhost, channel, changes.ToArray()));
            }
            else
            {
                ModeChange[] changes = new ModeChange[number_of_targets];
                string modes = message.Parameters[1];
                bool adding = false;
                int modecursor = 0;
                foreach(char mode in modes)
                {
                    switch (mode)
                    {
                        case '+': adding = true;
                            break;
                        case '-': adding = false;
                            break;
                        default:
                            changes[modecursor].IsAdded = adding;
                            changes[modecursor].Mode = mode;
                            changes[modecursor].Name = message.Parameters[2 + modecursor];
                            changes[modecursor].IsGlobalMode = false;
                            modecursor++;
                            break;
                    }
                }
                if(this.OnMode != null)
                    OnMode(this, new IrcClientOnModeEventArgs(userid, nick, userhost, channel, changes));
            }

        }


        // Command methods
        public void Join(string channel)
        {
            SendLine("JOIN {0}", channel);
        }
        public void Nick(string nick)
        {
            SendLine("NICK {0}", nick);
        }
        public void Oper(string message)
        {
            SendLine("OPER {0}", message);
        }
        public void Part(string channel)
        {
            SendLine("PART {0}", channel);
        }
        
        public void PrivMsg(string destination, string message)
        {
            SendLine("PRIVMSG {0} :{1}", destination, message);
        }
        public void PrivMsg(string destination, string format,  params object[] arg)
        {
            this.PrivMsg(destination, string.Format(Thread.CurrentThread.CurrentCulture, format, arg));
        }
        public void Say(string channel, string message)
        {
            PrivMsg(channel, message);
        }
        public void Say(string destination, string format, params object[] arg)
        {
            this.PrivMsg(destination, string.Format(Thread.CurrentThread.CurrentCulture, format, arg));
        }

        public void Quit(string reason)
        {
            SendLine("QUIT :{0}", reason);
            Log(string.Format("Quitting with reason : {0}", reason), MessageLevel.Info);
            Close();
        }
        public void User(string username, string hostname, string servername, string realname)
        {
            SendLine("USER {0} {1} {2} :{3}", username, hostname, servername, realname);
        }
        public void Pass(string password)
        {
            SendLine("PASS {0}", password);
        }
        public void Who(string channel)
        {
            SendLine("WHO {0}", channel);
        }
        public void NickServ(string message)
        {
            PrivMsg("NickServ", message);
        }
        public void HostServ(string message)
        {
            PrivMsg("HostServ", message);
        }
        public void CapabilityRequest(string capacityRequested)
        {
            SendLine("CAP REQ :{0}", capacityRequested);
        }


        private bool m_LogEnabled = true;
        public bool LogEnabled { get { return m_LogEnabled; } set { m_LogEnabled = value; } }
        private bool m_LogToConsole = true;
        public bool LogToConsole { get { return m_LogToConsole; } set { m_LogToConsole = value; } }
        private bool m_LogIncludeTimestamp = true;
        public bool LogIncludeTimestamp { get { return m_LogIncludeTimestamp; } set { m_LogIncludeTimestamp = value; } }

        private void Log(string message, MessageLevel level)
        {
            if (m_LogEnabled)
            {
                if (level >= LogLevel)
                {
                    if (m_LogToConsole)
                    {
                        if (m_LogIncludeTimestamp)
                            Console.WriteLine(string.Concat("[", DateTime.Now.ToString(), "] ", message));
                        else
                            Console.WriteLine(message);
                    }
                    else
                    {
                        if (OnLog != null)
                        {
                            if (m_LogIncludeTimestamp)
                                OnLog(this, new IrcClientOnLogEventArgs(string.Concat("[", DateTime.Now.ToString(), "] ", message), level));
                            else
                                OnLog(this, new IrcClientOnLogEventArgs(message, level));
                        }
                    }
                }
            }
        }
    }
}