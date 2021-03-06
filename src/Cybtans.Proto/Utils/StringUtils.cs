using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;

namespace Cybtans.Proto.Utils
{
    public static class StringExtensions
    {        
        public static string Capitalize(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;

            if (s.Length == 1)
                return s.ToUpper();

            var firstLetter = s[0];

            return char.ToUpperInvariant(firstLetter) + s.Substring(1);
        }
     
        public static string Uncapitalize(this string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;

            if (s.Length == 1)
                return s.ToLowerInvariant();

            var firstLetter = s[0];

            return char.ToLowerInvariant(firstLetter) + s.Substring(1);
        }

        public static string Upper(this string s)
        {
            return s.ToUpperInvariant();
        }

        public static string Lower(this string s)
        {
            return s.ToLowerInvariant();
        }

        public static string Pascal(this string s)
        {
            var sections = s.Split('_');
            StringBuilder sb = new StringBuilder();
            foreach (var part in sections)
            {
                for (int i = 0; i < part.Length; i++)
                {
                    var c = part[i];
                    if (i == 0)
                    {
                        sb.Append(char.ToUpperInvariant(c));
                    }
                    else if (i <= part.Length - 1 && (char.IsLower(part[i - 1]) || char.IsDigit(part[i - 1])) && char.IsUpper(c))
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append(char.ToLowerInvariant(c));
                    }

                }
            }

            var result = sb.ToString();
            if (result.All(x => char.IsDigit(x)))
            {
                result = "_" + result;
            }

            return result;
        }

        public static string Camel(this string s)
        {
            StringBuilder sb = new StringBuilder();
            var sections = s.Split('_');

            if (sections.Length > 0)
            {
                foreach (var part in sections)
                {
                    for (int i = 0; i < part.Length; i++)
                    {
                        var c = part[i];
                        if (i == 0)
                        {
                            sb.Append(char.ToLowerInvariant(c));
                        }
                        else if (i <= part.Length - 1 && (char.IsLower(part[i - 1]) || char.IsDigit(part[i - 1])) && char.IsUpper(c))
                        {
                            sb.Append(c);
                        }
                        else
                        {
                            sb.Append(char.ToLowerInvariant(c));
                        }

                    }
                }
            }

            var result = sb.ToString();
            if (result.All(x => char.IsDigit(x)))
            {
                result = "_" + result;
            }

            return result;
        }

        public static string Pluralize(this string s)
        {
            if (s.EndsWith('y'))
            {
                return s[0..^1] + "ies";
            }
            else if (!s.EndsWith('s'))
            {
                return s + "s";
            }
            else
            {
                return s;
            }
        }

        public static string Scape(this string s)
        {
            return s.Replace("\"", "\\\"");
        }

        public static string[] GetAttributeList(this string s)
        {
            var items = s.Split(";");
            return items.Select(x => x.Trim()).ToArray();
            
        }
    }
}
