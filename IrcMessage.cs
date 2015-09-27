using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Irc
{
    public class IrcMessage
    {
        public string Prefix;
        public string Command;
        public string[] Parameters;
        public string Extra;

        public IrcMessage(string message_)
        {
            // Thanks to 
            string message = message_;
            int prefixEnd = -1, trailingStart = message.Length;
            string trailing = null;
            Prefix = Command = String.Empty;
            Parameters = new string[] { };

            // Grab the prefix if it is present. If a message begins
            // with a colon, the characters following the colon until
            // the first space are the prefix.
            if (message.StartsWith(":"))
            {
                prefixEnd = message.IndexOf(" ");
                Prefix = message.Substring(1, prefixEnd - 1);
            }
            else
            {
                // Message didn't started with ":", let's try to find one
                int extra = message.IndexOf(" :");
                if (extra > -1)
                {
                    Extra = message.Substring(0, extra-1);
                    message = message.Substring(extra + 1);
                    prefixEnd = message.IndexOf(" ");
                    Prefix = message.Substring(1, prefixEnd - 1);
                }
                else
                {
                    return; // Not an IRC message
                }
            }

            // Grab the trailing if it is present. If a message contains
            // a space immediately following a colon, all characters after
            // the colon are the trailing part.
            trailingStart = message.IndexOf(" :");
            if (trailingStart >= 0)
                trailing = message.Substring(trailingStart + 2);
            else
                trailingStart = message.Length;

            // Use the prefix end position and trailing part start
            // position to extract the command and parameters.
            var commandAndParameters = message.Substring(prefixEnd + 1, trailingStart - prefixEnd - 1).Split(' ');

            // The command will always be the first element of the array.
            Command = commandAndParameters.First();

            // The rest of the elements are the parameters, if they exist.
            // Skip the first element because that is the command.
            if (commandAndParameters.Length > 1)
                Parameters = commandAndParameters.Skip(1).ToArray();

            // If the trailing part is valid add the trailing part to the
            // end of the parameters.
            if (!String.IsNullOrEmpty(trailing))
                Parameters = Parameters.Concat(new string[] { trailing }).ToArray();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("[{0}] [{1}] [", Prefix, Command);
            foreach (string s in Parameters)
            {
                sb.AppendFormat("{0},", s);
            }
            sb.AppendLine("]");
            return sb.ToString();
        }
    }
}
