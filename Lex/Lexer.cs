namespace Lex;

using System.Globalization;
using System.Text.RegularExpressions;

public readonly record struct Token{
    public enum Type{
        ID,
        ARGC,
        FALSE,
        TRUE,
        INT_LIT,
        FLOAT_LIT,
        STR_LIT,

        COLON,
        SEMICOLON,
        DOT2,
        LPAREN,
        RPAREN,
        LBRACE,
        RBRACE,

        EQUALS,
        NOT_EQUALS,
        LESS_THAN,
        LESS_THAN_EQ,
        GREATER_THAN,
        GREATER_THAN_EQ,
        PLUS,
        MINUS,
        ASTERISK1,
        ASTERISK2,
        SLASH,
        PERCENT,
        SHIFT_LEFT,
        SHIFT_RIGHT,
        AMPERSAND,
        PIPE,
        CARET,
        TILDE,
        AND,
        OR,
        NOT,
        EQ,
        
        LET_DECL,

        BOOL,
        INT,
        FLOAT,

        PRINT,
        SCAN,

        ARGV,

        IF,
        ELSE,

        WHILE,
        FOR,

        RETURN
    }

    public readonly int line_number{ get; init; }
    public required readonly Type type{ get; init; }
    public required readonly string id{ get; init; }
}

public class Syntax_error_exception : Exception{
    public Syntax_error_exception(string msg) : base(msg.colour_str()){}
    public Syntax_error_exception(string msg, Exception inner_exception) : base(msg.colour_str(), inner_exception){}

    public Syntax_error_exception(){}
}

public static class Lexer{
    extension(string self){
        public string colour_str(byte r, byte g, byte b) => $"\x1b[38;2;{r};{g};{b}m{self}\x1b[0m";
        public string colour_str() => self.colour_str(255, 0, 0);
    }

    public static ReadOnlySpan<Token> tokenize(string path){
        List<Token> tokens = new();

        if (!path.EndsWith(".omni"))
            throw new ArgumentOutOfRangeException("Bad file extension");

        string file_lines = File.ReadAllText(path);

        if (!((Func<bool>)(() => {
            bool quote_is_closed = true;
            bool was_escaped = false;
            foreach (char c in file_lines){
                if (!was_escaped){
                    if (c == '\\')
                        was_escaped = true;
                    else if (c == '"')
                        quote_is_closed = !quote_is_closed;
                }
                else
                    was_escaped = false;
            }
            return quote_is_closed;
        }))())
            throw new Syntax_error_exception($"Unclosed string literal");

        int line_idx = 0;
        foreach (
            string line in
            Regex.Split(
                file_lines,
                "(" +
                    @"/\*/(?:\r\n|\r|\n|.)*?/\*/|(?://.*)?(?:\r\n|\r|\n)|""(?:[^""\\]|\\.)*?""|" +
                    @"\.\.|\*\*|[:;(){}+*/%&|^~-]|<<|>>|!=|[<>=]=?|" +
                    @"\bargc\b|\bfalse\b|\btrue\b|\band\b|\bor\b|\bnot\b|\blet\b|\bbool\b|\bint\b|\bfloat\b|\bprint\b|\bscan\b|\bargv\b|\bif\b|\belse\b|\bwhile\b|\bfor\b|\breturn\b" +
                ")"
            ).Select((s) => s.Trim(' ')).Where((s) => s.Length > 0).ToArray()
        ){
            // Console.WriteLine($"tok: {System.Text.RegularExpressions.Regex.Escape(line)} | tok.Length: {line.Length}");
            if (line != Environment.NewLine && !line.StartsWith("//") && !line.StartsWith("/*/")){
                string token_id = line;

                Token.Type token_type = line switch{
                    "argc"   => Token.Type.ARGC,
                    "false"  => Token.Type.FALSE,
                    "true"   => Token.Type.TRUE,

                    ":"      => Token.Type.COLON,
                    ";"      => Token.Type.SEMICOLON,
                    ".."     => Token.Type.DOT2,
                    "("      => Token.Type.LPAREN,
                    ")"      => Token.Type.RPAREN,
                    "{"      => Token.Type.LBRACE,
                    "}"      => Token.Type.RBRACE,

                    "=="     => Token.Type.EQUALS,
                    "!="     => Token.Type.NOT_EQUALS,
                    "<"      => Token.Type.LESS_THAN,
                    "<="     => Token.Type.LESS_THAN_EQ,
                    ">"      => Token.Type.GREATER_THAN,
                    ">="     => Token.Type.GREATER_THAN_EQ,
                    "+"      => Token.Type.PLUS,
                    "-"      => Token.Type.MINUS,
                    "*"      => Token.Type.ASTERISK1,
                    "**"     => Token.Type.ASTERISK2,
                    "/"      => Token.Type.SLASH,
                    "%"      => Token.Type.PERCENT,
                    "<<"     => Token.Type.SHIFT_LEFT,
                    ">>"     => Token.Type.SHIFT_RIGHT,
                    "&"      => Token.Type.AMPERSAND,
                    "|"      => Token.Type.PIPE,
                    "^"      => Token.Type.CARET,
                    "~"      => Token.Type.TILDE,
                    "and"    => Token.Type.AND,
                    "or"     => Token.Type.OR,
                    "not"    => Token.Type.NOT,
                    "="      => Token.Type.EQ,

                    "let"    => Token.Type.LET_DECL,

                    "bool"   => Token.Type.BOOL,
                    "int"    => Token.Type.INT,
                    "float"  => Token.Type.FLOAT,

                    "print"  => Token.Type.PRINT,
                    "scan"   => Token.Type.SCAN,

                    "argv"   => Token.Type.ARGV,

                    "if"     => Token.Type.IF,
                    "else"   => Token.Type.ELSE,

                    "while"  => Token.Type.WHILE,
                    "for"    => Token.Type.FOR,

                    "return" => Token.Type.RETURN,

                    _ => ((Func<Token.Type>)(() => {
                        if (line.All((c) => char.IsDigit(c)))
                            return Token.Type.INT_LIT;
                        else if ((line.StartsWith("0x") || line.StartsWith("0X")) && line[2..].All((c) => char.IsAsciiHexDigit(c))){
                            token_id = int.Parse(line[2..], NumberStyles.AllowHexSpecifier).ToString();
                            return Token.Type.INT_LIT;
                        }
                        else if ((line.StartsWith("0b") || line.StartsWith("0B")) && line[2..].All((c) => c == '0' || c == '1')){
                            token_id = int.Parse(line[2..], NumberStyles.AllowBinarySpecifier).ToString();
                            return Token.Type.INT_LIT;
                        }
                        else if (line.Count((c) => c == '.') == 1 && line[0] != '.' && line.All((c) => char.IsDigit(c) || c == '.'))
                            return Token.Type.FLOAT_LIT;
                        else if (line[0] == '"'){
                            token_id = line[1..(line.Length - 1)];
                            return Token.Type.STR_LIT;
                        }
                        else if (!char.IsDigit(line[0]) && line.All((c) => char.IsAsciiLetter(c) || char.IsDigit(c) || c == '_'))
                            return Token.Type.ID;
                        else
                            throw new Syntax_error_exception($"On line <{line_idx + 1}> found invalid token <{line}>");
                    }))(),
                };
                if (tokens.Count > 0 && tokens[^1].type == Token.Type.STR_LIT && token_type == Token.Type.STR_LIT)
                    tokens[^1] = tokens[^1] with{id = tokens[^1].id + line[1..(line.Length - 1)]};
                else
                    tokens.Add(new(){type = token_type, id = token_id, line_number = line_idx + 1});
            }
            else
                line_idx += Regex.Count(line, @"\r\n|\r|\n");
        }

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(tokens);
    }
}
