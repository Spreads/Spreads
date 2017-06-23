using System;
using System.IO;
using System.Text;

namespace ILSub
{
    internal class Program
    {
        private class Match
        {
            public int Index { get; set; }
            public int EndIndex { get; set; }
        }

        private static Match FindContainingAllTokensInOrder(string s, int startIndex, int endIndex, string[] tokens)
        {
            int firstTokenStart;
            int lastTokenEnd;

            if (tokens.Length == 0)
            {
                return null;
            }
            firstTokenStart = s.IndexOf(tokens[0], startIndex, endIndex - startIndex);
            if (firstTokenStart == -1)
            {
                return null;
            }

            lastTokenEnd = firstTokenStart + tokens[0].Length;
            for (int i = 1; i < tokens.Length; i++)
            {
                lastTokenEnd = s.IndexOf(tokens[i], lastTokenEnd, endIndex - lastTokenEnd);
                if (lastTokenEnd == -1)
                {
                    return null;
                }
                lastTokenEnd += tokens[i].Length;
            }

            Match ret = new Match();
            ret.Index = firstTokenStart;
            ret.EndIndex = lastTokenEnd;
            return ret;
        }

        private static Match FindILSubClass(string s)
        {
            string[] tokens = new string[]
            {
                @".class private auto ansi beforefieldinit System.ILSub",
                "} // end of class System.ILSub",
            };

            return FindContainingAllTokensInOrder(s, 0, s.Length, tokens);
        }

        private static Match FindMethod(string s, int startIndex, int endIndex)
        {
            string[] tokens = new string[]
            {
                @".method ",
                "} // end of method",
            };

            return FindContainingAllTokensInOrder(s, startIndex, endIndex, tokens);
        }

        private static string StringFromMatch(string s, Match match)
        {
            return s.Substring(match.Index, match.EndIndex - match.Index);
        }

        private static Match GetMethodBody(string s, Match methodDef)
        {
            if (methodDef == null)
            {
                return null;
            }

            Match ret = new Match();
            ret.Index = s.IndexOf("{", methodDef.Index, methodDef.EndIndex - methodDef.Index);
            ret.EndIndex = s.LastIndexOf("  } // end of method", methodDef.EndIndex - 1, methodDef.EndIndex - methodDef.Index);
            //string x = StringFromMatch(methodDef);

            if (ret.Index == -1 || ret.EndIndex == -1)
            {
                return null;
            }
            else
            {
                ret.Index = ret.Index + 1;

                return ret;
            }
        }

        private static Match FindILSubInstance(string s, int startIndex, int endIndex)
        {
            string[] tokens = new string[]
            {
                @".custom instance void System.ILSub::.ctor(string) = {string('",
                "')}\r\n"
            };

            return FindContainingAllTokensInOrder(s, startIndex, endIndex, tokens);
        }

        private static Match FindILSubText(string s, int startIndex, int endIndex)
        {
            string[] tokens = new string[]
            {
                "{string('",
                "')}\r\n"
            };

            Match text = FindContainingAllTokensInOrder(s, startIndex, endIndex, tokens);

            if (text != null)
            {
                text.Index = text.Index + tokens[0].Length;
                text.EndIndex = text.EndIndex - tokens[tokens.Length - 1].Length;
            }

            return text;
        }

        private static bool FindMethodWithILSubInstance(string s, int startIndex, int endIndex, out Match methodBody, out Match ilText)
        {
            while (true)
            {
                Match method = FindMethod(s, startIndex, endIndex);
                if (method == null)
                {
                    methodBody = null;
                    ilText = null;
                    return false;
                }
                startIndex = method.EndIndex;

                string dbg1 = StringFromMatch(s, method);

                methodBody = GetMethodBody(s, method);
                if (methodBody == null)
                {
                    ilText = null;
                    continue;
                }

                string dbg2 = StringFromMatch(s, methodBody);

                Match ilSub = FindILSubInstance(s, methodBody.Index, methodBody.EndIndex);
                if (ilSub == null)
                {
                    continue;
                }
                string dbg3 = StringFromMatch(s, ilSub);

                methodBody.Index = ilSub.Index;

                ilText = FindILSubText(s, ilSub.Index, ilSub.EndIndex);
                if (ilText == null)
                {
                    throw new Exception("internal error");
                }

                string dbg4 = StringFromMatch(s, ilText);

                return true;
            }
        }

        private static string RewriteIL(string input)
        {
            Match ilSubClass = FindILSubClass(input);
            if (ilSubClass == null)
            {
                throw new Exception("ILSub class not found");
            }

            input = input.Substring(0, ilSubClass.Index) + input.Substring(ilSubClass.EndIndex);

            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < input.Length)
            {
                Match methodBody;
                Match ilText;
                if (!FindMethodWithILSubInstance(input, i, input.Length, out methodBody, out ilText))
                {
                    sb.Append(input, i, input.Length - i);
                    return sb.ToString();
                }

                sb.Append(input, i, methodBody.Index - i);

                var ilSb = new StringBuilder(input, ilText.Index, ilText.EndIndex - ilText.Index, ilText.EndIndex - ilText.Index);
                ilSb.Replace(@"\n", "\n").Replace(@"\r", "\r").Replace(@"\t", "\t");

                sb.Append(ilSb);
                sb.AppendLine();
                i = methodBody.EndIndex;
            }
            return sb.ToString();
        }

        private static string AddAILAttribute(string input)
        {
            //var sb = new StringBuilder();
            var position = 0;
            var idx = 0;

            while ((idx = input.IndexOf("RewriteAILAttribute", position)) >= 0)
            {
                position = idx + "RewriteAILAttribute  aggressiveinlining".Length;
                idx = idx - 40;
                while (!input.Substring(idx, 11).StartsWith("cil managed"))
                {
                    idx--;
                }

                input = input.Substring(0, idx) + "cil managed aggressiveinlining" +
                             input.Substring(idx + "cil managed".Length);
            }

            return input;
        }

        private static void Main(string[] args)
        {
            if (args == null || args.Length != 2)
            {
                string input = File.ReadAllText(@"C:\MD\Dev\fi.im\sln\Spreads\bin\net451\Spreads.Collections.beforerewrite.il");
                var result = AddAILAttribute(input);
                File.WriteAllText(@"C:\MD\Dev\fi.im\sln\Spreads\bin\net451\Spreads.Collections.rewritten.il", result);
                Console.WriteLine("Usage ILSub.exe <input.il> <output.il>");
            }
            else
            {
                string input = File.ReadAllText(args[0]);
                string result = input;
                Match ilsubclass = FindILSubClass(input);

                if (ilsubclass != null)
                {
                    result = RewriteIL(result);
                }

                result = AddAILAttribute(result);

                File.WriteAllText(args[1], result);
            }
        }
    }
}