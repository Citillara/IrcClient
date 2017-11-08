using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Irc
{
    public class IrcMessage
    {
        /*
         * <message>       ::= ['@' <tags> <SPACE>] [':' <prefix> <SPACE> ] <command> <params> <crlf>
         * <tags>          ::= <tag> [';' <tag>]*
         * <tag>           ::= <key> ['=' <escaped value>]
         * <key>           ::= [ <vendor> '/' ] <sequence of letters, digits, hyphens (`-`)>
         * <escaped value> ::= <sequence of any characters except NUL, CR, LF, semicolon (`;`) and SPACE>
         * <vendor>        ::= <host>
         * 
         * @aaa=bbb;ccc;example.com/ddd=eee :nick!ident@host.com PRIVMSG me :Hello world
         */


        public string Prefix { get; set; }
        public string Command { get; set; }
        public string[] Parameters = new string[]{ };
        public Dictionary<string, string> Tags = new Dictionary<string,string>();

        public static readonly char[] TAG_SEPARATOR = new char[] { ';' };

        public IrcMessage(string message_)
        {
            string message = message_;
            if (message.StartsWith("@")) // We have tags
            {
                int next_space = message.IndexOf(' ');

                // We grab the tags
                string tags = message.Substring(1, next_space-1);

                // We cut the message remaining part
                message = message.Substring(next_space + 1);
                
                // Listring the tags
                string[] split = tags.Split(TAG_SEPARATOR);

                foreach (string tag in split)
                {
                    int separator = tag.IndexOf('=');
                    if (separator == -1)
                    {
                        Tags.Add(UnescapeTag(tag), string.Empty);
                        continue;
                    }
                    int length = tag.Length;
                    string key = tag.Substring(0, separator);
                    string value = string.Empty;
                    if (length > separator + 1)
                        value = tag.Substring(separator + 1);
                    Tags.Add(UnescapeTag(key), UnescapeTag(value));
                }
            }

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

            // Grab the trailing if it is present. If a message contains
            // a space immediately following a colon, all characters after
            // the colon are the trailing part.
            trailingStart = message.IndexOf(" :", StringComparison.InvariantCulture);
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

        /*
         * Character        Sequence in <escaped value>
         * ; (semicolon)    \: (backslash and colon)
         * SPACE            \s
         * \                \\
         * CR               \r
         * LF               \n
         * all others       the character itself
         */

        public string UnescapeTag(string tag)
        {
            return tag
                .Replace(@"\:", ";")
                .Replace(@"\s", " ")
                .Replace(@"\\", @"\")
                .Replace(@"\r", "\r")
                .Replace(@"\n", "\n");
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Pre [{0}] Com [{1}] Param [", Prefix, Command);
            foreach (string s in Parameters)
            {
                sb.AppendFormat("{0},", s);
            }
            sb.Append("] Tags [");
            foreach (KeyValuePair<string, string> kp in Tags)
            {
                sb.AppendFormat("{0} = {1},", kp.Key, kp.Value);
            }
            sb.AppendLine("]");
            return sb.ToString();
        }
    }
}
