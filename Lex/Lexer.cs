namespace Lex;

public readonly struct Token{
    public enum Type{
        IDENTIFIER,

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
        GREATER_THAN,
        EXCLAMATION_MARK,
        EQUALS,
        PLUS,
        MINUS,
        ASTERISK,
        SLASH,
        PERCENT,
        
        LET_DECL,

        BOOL,
        FALSE,
        TRUE,

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

    public required readonly Type type{ get; init; }
    public required readonly string id{ get; init; }
}

public sealed class Lexer{
    List<Token> m_tokens;

    public Lexer(string path){
        m_tokens = new();

        foreach (string line in File.ReadLines(path)){
            List<string> line_splitted = System.Text.RegularExpressions.Regex.Split(
                line,
                @"([:;(){}""<>!=+*/%-]|\blet\b|\bbool\b|\bfalse\b|\btrue\b|\bint\b|\bfloat\b|\bprint\b|\bscan\b|\bif\b|\belse\b|\bwhile\b|\band\b|\bor\b|\bnot\b|\breturn\b)"
            )
            .Where((s) => !string.IsNullOrWhiteSpace(s)).ToList();

            for (int i = 0, j; i < line_splitted.Count && (j = line_splitted.FindIndex(i, (s) => (s.Trim() == "\""))) >= 0; i = j + 1){
                List<string> remaining_tokens = line_splitted[(j + 1)..];

                int tokens_to_concat = remaining_tokens.IndexOf("\"") + 1;
                if (tokens_to_concat == 0)
                    throw new Exception("String literal was not closed");

                string str_lit = line_splitted[j];
                for (int k = 0; k < tokens_to_concat; ++k)
                    str_lit += remaining_tokens[k];

                line_splitted.RemoveRange(j, tokens_to_concat + 1);
                line_splitted.Insert(j, str_lit);
            }

            line_splitted = line_splitted.Select((s) => s.Trim()).ToList();

            foreach (string s in line_splitted){
                Token.Type token_type;
                switch (s){
                    case ":":      token_type = Token.Type.COLON;            break;
                    case ";":      token_type = Token.Type.SEMICOLON;        break;
                    case "(":      token_type = Token.Type.LPAREN;           break;
                    case ")":      token_type = Token.Type.RPAREN;           break;
                    case "{":      token_type = Token.Type.LBRACE;           break;
                    case "}":      token_type = Token.Type.RBRACE;           break;

                    case "<":      token_type = Token.Type.LESS_THAN;        break;
                    case ">":      token_type = Token.Type.GREATER_THAN;     break;
                    case "!":      token_type = Token.Type.EXCLAMATION_MARK; break;
                    case "=":      token_type = Token.Type.EQUALS;           break;
                    case "+":      token_type = Token.Type.PLUS;             break;
                    case "*":      token_type = Token.Type.ASTERISK;         break;
                    case "/":      token_type = Token.Type.SLASH;            break;
                    case "%":      token_type = Token.Type.PERCENT;          break;
                    case "-":      token_type = Token.Type.MINUS;            break;

                    case "let":    token_type = Token.Type.LET_DECL;         break;

                    case "bool":   token_type = Token.Type.BOOL;             break;
                    case "false":  token_type = Token.Type.FALSE;            break;
                    case "true":   token_type = Token.Type.TRUE;             break;

                    case "int":    token_type = Token.Type.INT;              break;
                    case "float":  token_type = Token.Type.FLOAT;            break;

                    case "print":  token_type = Token.Type.PRINT;            break;
                    case "scan":   token_type = Token.Type.SCAN;             break;

                    case "if":     token_type = Token.Type.SCAN;             break;
                    case "else":   token_type = Token.Type.ELSE;             break;

                    case "while":  token_type = Token.Type.IF;               break;

                    case "and":    token_type = Token.Type.AND;              break;
                    case "or":     token_type = Token.Type.OR;               break;
                    case "not":    token_type = Token.Type.NOT;              break;

                    case "return": token_type = Token.Type.RETURN;           break;

                    default:
                        if (char.IsAsciiLetter(s[0]))
                            token_type = Token.Type.IDENTIFIER;
                        else if (s.All((c) => char.IsDigit(c)))
                            token_type = Token.Type.INT_LIT;
                        else if (s.Count((c) => c == '.') == 1 && s[0] != '.' && s.All((c) => char.IsDigit(c) || c == '.'))
                            token_type = Token.Type.FLOAT_LIT;
                        else if (s[0] == '"')
                            token_type = Token.Type.STR_LIT;
                        else
                            throw new Exception($"{s}");
                        break;
                }
                m_tokens.Add(new(){type = token_type, id = s});
            }
        }
    }
}
