using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Irc
{
    public class IrcClientOnJoinEventArgs : EventArgs
    {
        public string UserID;
        public string Name;
        public string Userhost;
        public string Channel;
        public bool IsMyself;
        public IrcClientOnJoinEventArgs(string name, string userid, string userhost, string channel, bool isMyself)
        {
            UserID = userid;
            Name = name;
            Userhost = userhost;
            Channel = channel;
            IsMyself = isMyself;
        }
        public override string ToString()
        {
            return string.Format("{0} {1}!{2}@{3} : {4}", Channel, Name, UserID, Userhost);
        }
    }

    public class IrcClientOnPrivateMessageEventArgs : EventArgs
    {
        public string UserID;
        public string Name;
        public string Userhost;
        public string Message;
        public string Channel;
        public bool IsToMe;
        public Dictionary<string, string> Tags;
        public IrcClientOnPrivateMessageEventArgs(string message, string name, string userid, string userhost,
            string channel, bool isToMe, Dictionary<string, string> tags)
        {
            UserID = userid;
            Message = message;
            Name = name;
            Userhost = userhost;
            Channel = channel;
            IsToMe = isToMe;
            Tags = tags;
        }
        public override string ToString()
        {
            return string.Format("{0} {1}!{2}@{3} : {4}", Channel, Name, UserID, Userhost, Message);
        }
    }

    public class IrcClientOnPartEventArgs : EventArgs
    {
        public string UserID;
        public string Name;
        public string Userhost;
        public string Channel;
        public bool IsMyself;
        public IrcClientOnPartEventArgs(string name, string userid, string userhost, string channel, bool isMyself)
        {
            UserID = userid;
            Name = name;
            Userhost = userhost;
            Channel = channel;
            IsMyself = isMyself;
        }
        public override string ToString()
        {
            return string.Format("{0} {1}!{2}@{3} : {4}", Channel, Name, UserID, Userhost);
        }
    }
    public class IrcClientOnQuitEventArgs : EventArgs
    {
        public string UserID;
        public string Name;
        public string Userhost;
        public string Channel;
        public bool IsMyself;
        public IrcClientOnQuitEventArgs(string name, string userid, string userhost, string channel, bool isMyself)
        {
            UserID = userid;
            Name = name;
            Userhost = userhost;
            Channel = channel;
            IsMyself = isMyself;
        }
        public override string ToString()
        {
            return string.Format("{0} {1}!{2}@{3} : {4}", Channel, Name, UserID, Userhost);
        }
    }


    public class IrcClientOnChannelNickListReceivedEventArgs : EventArgs
    {
        public string Channel;
        public string[] NameList;
        public IrcClientOnChannelNickListReceivedEventArgs(string channel, string[] nameList)
        {
            Channel = channel;
            NameList = nameList;
        }
        public override string ToString()
        {
            return string.Format("{0} : {1} users", Channel, NameList.Length);
        }
    }

    public class IrcClientOnModeEventArgs : EventArgs
    {
        public string UserID;
        public string Name;
        public string Userhost;
        public string Channel;
        public bool IsMyself;
        public ModeChange[] ModeChanges;

        public IrcClientOnModeEventArgs(string userid, string name, string userhost, string channel, ModeChange[] modeChanges)
        {
            UserID = userid;
            Name = name;
            Userhost = userhost;
            Channel = channel;
            ModeChanges = modeChanges;
        }
    }


    public class IrcClientOnLogEventArgs : EventArgs
    {
        private string m_message;
        public string Message { get { return m_message; } }
        private MessageLevel m_level;
        public MessageLevel Level { get { return m_level; } }

        public IrcClientOnLogEventArgs(string message, MessageLevel level)
        {
            m_message = message;
            m_level = level;
        }
    }

}
