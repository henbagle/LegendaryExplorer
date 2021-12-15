﻿using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Matching;
using LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing
{
    public class StringLexer
    {
        //private readonly List<TokenMatcherBase> TokenMatchers;
        private readonly CharDataStream Data;
        private SourcePosition StreamPosition;
        private readonly MessageLog Log;

        private StringLexer(string code, MessageLog log = null)
        {
            Data = new CharDataStream(code);
            Log = log ?? new MessageLog();
            StreamPosition = new SourcePosition(1, 0, 0);

            //TokenMatchers = new List<TokenMatcherBase>();

            ////Do not reorder! This is the order in which the lexer should try each matcher
            //TokenMatchers.Add(new SingleLineCommentMatcher());
            //TokenMatchers.Add(new StringLiteralMatcher());
            //TokenMatchers.Add(new NameLiteralMatcher());
            //TokenMatchers.Add(new StringRefLiteralMatcher());
            //TokenMatchers.AddRange(GlobalLists.DelimitersAndOperators);
            //TokenMatchers.Add(new WhiteSpaceMatcher());
            //TokenMatchers.Add(new NumberMatcher());
            //TokenMatchers.Add(new WordMatcher());

        }

        public static List<ScriptToken> Lex(string code, MessageLog log = null)
        {
            var lexer = new StringLexer(code, log);
            return lexer.LexData();
        }

        //private ScriptToken GetNextToken()
        //{
        //    if (Data.AtEnd())
        //    {
        //        return new ScriptToken(TokenType.EOF, null, StreamPosition, StreamPosition);
        //    }

        //    ScriptToken result = null;
        //    foreach (TokenMatcherBase matcher in TokenMatchers)
        //    {
        //        Data.PushSnapshot();

        //        result = matcher.Match(Data, ref StreamPosition, Log);
        //        if (result == null)
        //        {
        //            Data.PopSnapshot();
        //        }
        //        else
        //        {
        //            Data.DiscardSnapshot();
        //            break;
        //        }
        //    }

        //    if (result == null)
        //    {
        //        Log.LogError($"Could not lex '{Data.CurrentItem}'",
        //            StreamPosition, StreamPosition.GetModifiedPosition(0, 1, 1));
        //        Data.Advance();
        //        return new ScriptToken(TokenType.INVALID, Data.CurrentItem.ToString(), StreamPosition, StreamPosition.GetModifiedPosition(0, 1, 1)) { SyntaxType = EF.ERROR };
        //    }
        //    return result;
        //}

        private ScriptToken GetNextTokenImproved()
        {
            if (Data.AtEnd())
            {
                return new ScriptToken(TokenType.EOF, null, StreamPosition, StreamPosition);
            }

            ScriptToken result = null;

            char peek = Data.CurrentItem;
            if (peek == '/' && Data.LookAhead(1) == '/')
            {
                result = SingleLineCommentMatcher.MatchComment(Data, ref StreamPosition);
            }
            else if (peek == '"')
            {
                result = StringLiteralMatcher.MatchString(Data, ref StreamPosition, Log);
            }
            else if (peek == '\'')
            {
                result = NameLiteralMatcher.MatchName(Data, ref StreamPosition, Log);
            }
            else if (peek == '$' && (Data.LookAhead(1).IsDigit() || 
                                     Data.LookAhead(1) == '-' && Data.LookAhead(2).IsDigit()))
            {
                result = StringRefLiteralMatcher.MatchStringRef(Data, ref StreamPosition);
            }
            else if (GlobalLists.IsDelimiterChar(peek))
            {
                //looping over every single one is a terrible way of doing this.
                foreach (SymbolMatcher matcher in GlobalLists.DelimitersAndOperators)
                {
                    Data.PushSnapshot();

                    result = matcher.Match(Data, ref StreamPosition, Log);
                    if (result == null)
                    {
                        Data.PopSnapshot();
                    }
                    else
                    {
                        Data.DiscardSnapshot();
                        break;
                    }
                }
            }
            else if (char.IsWhiteSpace(peek))
            {
                result = WhiteSpaceMatcher.MatchWhiteSpace(Data, ref StreamPosition);
            }
            else if (peek.IsDigit())
            {
                result = NumberMatcher.MatchNumber(Data, ref StreamPosition);
            }
            else
            {
                result = WordMatcher.MatchWord(Data, ref StreamPosition);
            }

            

            if (result == null)
            {
                Log.LogError($"Could not lex '{Data.CurrentItem}'",
                    StreamPosition, StreamPosition.GetModifiedPosition(0, 1, 1));
                Data.Advance();
                return new ScriptToken(TokenType.INVALID, Data.CurrentItem.ToString(), StreamPosition, StreamPosition.GetModifiedPosition(0, 1, 1)) { SyntaxType = EF.ERROR };
            }
            return result;
        }

        private List<ScriptToken> LexData()
        {
            var tokens = new List<ScriptToken>();
            StreamPosition = new SourcePosition(1, 0, 0);
            ScriptToken token = GetNextTokenImproved();
            while (token.Type != TokenType.EOF)
            {
                if (token.Type is not TokenType.WhiteSpace and not TokenType.SingleLineComment and not TokenType.MultiLineComment)
                {
                    tokens.Add(token);
                }

                token = GetNextTokenImproved();
            }
            return tokens;
        }
    }
}
