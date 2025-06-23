﻿using System;
using System.Globalization;

namespace Wombat.Network.WebSockets
{
    internal sealed class WebSocketHelpers
    {
        internal static bool FindHttpMessageTerminator(byte[] buffer, int offset, int count, out int index)
        {
            index = -1;

            for (int i = 0; i < count; i++)
            {
                if (i + Consts.HttpMessageTerminator.Length <= count)
                {
                    bool matched = true;
                    for (int j = 0; j < Consts.HttpMessageTerminator.Length; j++)
                    {
                        if (buffer[offset + i + j] != Consts.HttpMessageTerminator[j])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                    {
                        index = i;
                        return true;
                    }
                }
                else
                {
                    break;
                }
            }

            return false;
        }

        internal static bool ValidateSubprotocol(string subProtocol)
        {
            if (string.IsNullOrWhiteSpace(subProtocol))
                throw new ArgumentNullException("subProtocol");

            string separators = "()<>@,;:\\\"/[]?={} ";

            char[] chars = subProtocol.ToCharArray();
            string invalidChar = null;
            int i = 0;
            while (i < chars.Length)
            {
                char ch = chars[i];
                if (ch < 0x21 || ch > 0x7e)
                {
                    invalidChar = string.Format(CultureInfo.InvariantCulture, "[{0}]", (int)ch);
                    break;
                }

                if (!char.IsLetterOrDigit(ch) && separators.IndexOf(ch) >= 0)
                {
                    invalidChar = ch.ToString();
                    break;
                }

                i++;
            }

            return invalidChar == null;
        }
    }
}
