namespace Lex;

public static class Lex_extensions{
    extension(string self){
        public string colour_str(byte r, byte g, byte b) => $"\x1b[38;2;{r};{g};{b}m{self}\x1b[0m";
        public string colour_str() => $"\x1b[38;2;255;0;0m{self}\x1b[0m";
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

public sealed class Lexer{
    List<Token> m_tokens;

    public Lexer(string path){
        m_tokens = new();

        if (!path.EndsWith(".omni"))
            throw new Exception("Bad file extension".colour_str());

        string file_lines = File.ReadAllText(path);

        if (file_lines.Count((c) => c == '"') % 2 != 0)
            throw new Exception("String literal was not closed".colour_str());

        int line_number = 0;
        foreach (string line in file_lines.Split('\n')){
            List<string> line_splitted = System.Text.RegularExpressions.Regex.Split(
                line,
                @"([:;(){}<>!=+*/%-]|""(?:.*)""|\blet\b|\bbool\b|\bfalse\b|\btrue\b|\bint\b|\bfloat\b|\bprint\b|\bscan\b|\bif\b|\belse\b|\bwhile\b|\band\b|\bor\b|\bnot\b|\breturn\b)"
            )
            .Where((s) => !string.IsNullOrWhiteSpace(s)).ToList();

            for (
                int i = 0, j;
                i >= 0 && (j = line_splitted.FindIndex(i, (s) => { string temp = s.Trim(); return temp == "!" || temp == "=" || temp == "<" || temp == ">"; })) >= 0;
                i = j + 1
            ){
                if (j + 1 < line_splitted.Count){
                    string temp = line_splitted[j] + line_splitted[j + 1];
                    switch (line_splitted[j].Trim()){
                        case "!":
                            if (temp.Trim() != "!=")
                                throw new Exception("<!> must be followed by <=>".colour_str());
                            else{
                                line_splitted[j] = temp;
                                line_splitted.RemoveAt(j + 1);
                            }
                            break;
                        case "=":
                            if (temp.Trim() == "=="){
                                line_splitted[j] = temp;
                                line_splitted.RemoveAt(j + 1);
                            }
                            break;
                        case "<":
                            if (temp.Trim() == "<="){
                                line_splitted[j] = temp;
                                line_splitted.RemoveAt(j + 1);
                            }
                            break;
                        case ">":
                            if (temp.Trim() == ">="){
                                line_splitted[j] = temp;
                                line_splitted.RemoveAt(j + 1);
                            }
                            break;
                    }
                }
            }

            line_splitted = line_splitted.Select((s) => s.Trim()).ToList();

            foreach (string s in line_splitted){
                Token.Type token_type;
                switch (s){
                    case "false":  token_type = Token.Type.FALSE;           break;
                    case "true":   token_type = Token.Type.TRUE;            break;

                    case ":":      token_type = Token.Type.COLON;           break;
                    case ";":      token_type = Token.Type.SEMICOLON;       break;
                    case "(":      token_type = Token.Type.LPAREN;          break;
                    case ")":      token_type = Token.Type.RPAREN;          break;
                    case "{":      token_type = Token.Type.LBRACE;          break;
                    case "}":      token_type = Token.Type.RBRACE;          break;

                    case "<":      token_type = Token.Type.LESS_THAN;       break;
                    case "<=":     token_type = Token.Type.LESS_THAN_EQ;    break;
                    case ">":      token_type = Token.Type.GREATER_THAN;    break;
                    case ">=":     token_type = Token.Type.GREATER_THAN_EQ; break;
                    case "!=":     token_type = Token.Type.NOT_EQUALS;      break;
                    case "==":     token_type = Token.Type.EQUALS;          break;
                    case "=":      token_type = Token.Type.EQ;              break;
                    case "+":      token_type = Token.Type.PLUS;            break;
                    case "*":      token_type = Token.Type.ASTERISK;        break;
                    case "/":      token_type = Token.Type.SLASH;           break;
                    case "%":      token_type = Token.Type.PERCENT;         break;
                    case "-":      token_type = Token.Type.MINUS;           break;

                    case "let":    token_type = Token.Type.LET_DECL;        break;

                    case "bool":   token_type = Token.Type.BOOL;            break;
                    case "int":    token_type = Token.Type.INT;             break;
                    case "float":  token_type = Token.Type.FLOAT;           break;

                    case "print":  token_type = Token.Type.PRINT;           break;
                    case "scan":   token_type = Token.Type.SCAN;            break;

                    case "if":     token_type = Token.Type.SCAN;            break;
                    case "else":   token_type = Token.Type.ELSE;            break;

                    case "while":  token_type = Token.Type.IF;              break;

                    case "and":    token_type = Token.Type.AND;             break;
                    case "or":     token_type = Token.Type.OR;              break;
                    case "not":    token_type = Token.Type.NOT;             break;

                    case "return": token_type = Token.Type.RETURN;          break;

                    default:
                        if (s.All((c) => char.IsDigit(c)))
                            token_type = Token.Type.INT_LIT;
                        else if (s.Count((c) => c == '.') == 1 && s[0] != '.' && s.All((c) => char.IsDigit(c) || c == '.'))
                            token_type = Token.Type.FLOAT_LIT;
                        else if (s[0] == '"')
                            token_type = Token.Type.STR_LIT;
                        else if (!char.IsDigit(s[0]) && s.All((c) => char.IsAsciiLetter(c) || char.IsDigit(c) || c == '_'))
                            token_type = Token.Type.ID;
                        else
                            throw new Exception($"Found invalid token <{s}> on line <{line_number + 1}>".colour_str());
                        break;
                }

                m_tokens.Add(new(){type = token_type, id = s, line_number = line_number + 1});
            }
            ++line_number;
        }
    }

    public List<Token> get_tokens() => new(m_tokens);
}
