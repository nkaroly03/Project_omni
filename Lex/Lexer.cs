namespace Lex;

public static class Lex_extensions{
    extension(string self){
        public string colour_str(byte r, byte g, byte b) => $"\x1b[38;2;{r};{g};{b}m{self}\x1b[0m";
        public string colour_str() => self.colour_str(255, 0, 0);
    }
}

public readonly struct Token{
    public enum Type{
        ID,

        FALSE,
        TRUE,
        INT_LIT,
        FLOAT_LIT,
        STR_LIT,

        COLON,
        SEMICOLON,
        LPAREN,
        RPAREN,
        LBRACE,
        RBRACE,

        LESS_THAN,
        LESS_THAN_EQ,
        GREATER_THAN,
        GREATER_THAN_EQ,
        NOT_EQUALS,
        EQUALS,
        EQ,
        PLUS,
        MINUS,
        ASTERISK,
        SLASH,
        PERCENT,
        
        LET_DECL,

        BOOL,
        INT,
        FLOAT,

        PRINT,
        SCAN,

        IF,
        ELSE,

        WHILE,

        AND,
        OR,
        NOT,

        RETURN
    }

    public readonly int line_number{ get; init; }
    public required readonly Type type{ get; init; }
    public required readonly string id{ get; init; }

    public override string ToString() => $"{{.line_number = {line_number}, .type = {type}, .id = {id}}}";
}

public static class Lexer{
    public static List<Token> tokenize(string path){
        List<Token> tokens = new();

        if (!path.EndsWith(".omni"))
            throw new Exception("Bad file extension".colour_str());

        string file_lines = File.ReadAllText(path);

        if (!(
            (Func<bool>)(
                () => {
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
                }
            )
        )())
            throw new Exception("Unclosed string literal".colour_str());

        int line_idx = 0;
        foreach (
            string line in
            System.Text.RegularExpressions.Regex.Split(
                file_lines,
                @"([:;(){}+*/%-]|[<>!=]=?|(?:\r\n|\r|\n)|""(?:.*)""|\blet\b|\bbool\b|\bfalse\b|\btrue\b|\bint\b|\bfloat\b|\bprint\b|\bscan\b|\bif\b|\belse\b|\bwhile\b|\band\b|\bor\b|\bnot\b|\breturn\b)"
            ).Select((s) => s.Trim(' ')).Where((s) => s.Length > 0).ToArray()
        ){
            if (line != Environment.NewLine){
                Token.Type token_type = line switch{
                    "false"  => Token.Type.FALSE,
                    "true"   => Token.Type.TRUE,

                    ":"      => Token.Type.COLON,
                    ";"      => Token.Type.SEMICOLON,
                    "("      => Token.Type.LPAREN,
                    ")"      => Token.Type.RPAREN,
                    "{"      => Token.Type.LBRACE,
                    "}"      => Token.Type.RBRACE,

                    "<"      => Token.Type.LESS_THAN,
                    "<="     => Token.Type.LESS_THAN_EQ,
                    ">"      => Token.Type.GREATER_THAN,
                    ">="     => Token.Type.GREATER_THAN_EQ,
                    "!="     => Token.Type.NOT_EQUALS,
                    "=="     => Token.Type.EQUALS,
                    "="      => Token.Type.EQ,
                    "+"      => Token.Type.PLUS,
                    "*"      => Token.Type.ASTERISK,
                    "/"      => Token.Type.SLASH,
                    "%"      => Token.Type.PERCENT,
                    "-"      => Token.Type.MINUS,

                    "let"    => Token.Type.LET_DECL,

                    "bool"   => Token.Type.BOOL,
                    "int"    => Token.Type.INT,
                    "float"  => Token.Type.FLOAT,

                    "print"  => Token.Type.PRINT,
                    "scan"   => Token.Type.SCAN,

                    "if"     => Token.Type.IF,
                    "else"   => Token.Type.ELSE,

                    "while"  => Token.Type.WHILE,

                    "and"    => Token.Type.AND,
                    "or"     => Token.Type.OR,
                    "not"    => Token.Type.NOT,

                    "return" => Token.Type.RETURN,

                    _ => (
                        (Func<Token.Type>)(
                            () => {
                                if (line.All((c) => char.IsDigit(c)))
                                    return Token.Type.INT_LIT;
                                else if (line.Count((c) => c == '.') == 1 && line[0] != '.' && line.All((c) => char.IsDigit(c) || c == '.'))
                                    return Token.Type.FLOAT_LIT;
                                else if (line[0] == '"')
                                    return Token.Type.STR_LIT;
                                else if (!char.IsDigit(line[0]) && line.All((c) => char.IsAsciiLetter(c) || char.IsDigit(c) || c == '_'))
                                    return Token.Type.ID;
                                else
                                    throw new Exception($"On line <{line_idx + 1}> found invalid token <{line}>".colour_str());
                            }
                        )
                    )(),
                };
                if (tokens.Count > 0 && tokens.Last().type == Token.Type.STR_LIT && token_type == Token.Type.STR_LIT)
                    tokens[tokens.Count - 1] = tokens.Last() with{id = tokens.Last().id + line[1..(line.Length - 1)]};
                else
                    tokens.Add(new(){type = token_type, id = (token_type != Token.Type.STR_LIT) ? line : line[1..(line.Length - 1)], line_number = line_idx + 1});
            }
            else
                ++line_idx;
        }

        return tokens;
    }
}
