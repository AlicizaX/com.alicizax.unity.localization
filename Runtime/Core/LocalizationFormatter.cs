using System;
using Cysharp.Text;

namespace AlicizaX.Localization.Runtime
{
    public static class LocalizationFormatter
    {
        public static string Format(string format, object[] arguments)
        {
            if (arguments == null || arguments.Length == 0) return format;
            var builder = ZString.CreateStringBuilder();
            try
            {
                AppendFormat(ref builder, format, arguments);
                return builder.ToString();
            }
            finally { builder.Dispose(); }
        }

        public static void AppendFormat(ref Utf16ValueStringBuilder builder, string format, object[] arguments)
        {
            switch (arguments.Length)
            {
                case 1: builder.AppendFormat(format, arguments[0]); break;
                case 2: builder.AppendFormat(format, arguments[0], arguments[1]); break;
                case 3: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2]); break;
                case 4: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3]); break;
                case 5: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4]); break;
                case 6: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]); break;
                case 7: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6]); break;
                case 8: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7]); break;
                case 9: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8]); break;
                case 10: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8], arguments[9]); break;
                case 11: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8], arguments[9], arguments[10]); break;
                case 12: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8], arguments[9], arguments[10], arguments[11]); break;
                case 13: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8], arguments[9], arguments[10], arguments[11], arguments[12]); break;
                case 14: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8], arguments[9], arguments[10], arguments[11], arguments[12], arguments[13]); break;
                case 15: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8], arguments[9], arguments[10], arguments[11], arguments[12], arguments[13], arguments[14]); break;
                case 16: builder.AppendFormat(format, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5], arguments[6], arguments[7], arguments[8], arguments[9], arguments[10], arguments[11], arguments[12], arguments[13], arguments[14], arguments[15]); break;
                default: throw new FormatException("Localization supports at most 16 format arguments.");
            }
        }
    }
}
