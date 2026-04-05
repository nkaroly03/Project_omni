namespace Parse;

using Lex;

static class Parse_extensions{
    extension(Token.Type self){
        public bool is_atom() => (int)self >= (int)Token.Type.ID && (int)self <= (int)Token.Type.STR_LIT;
        public bool is_operation() => (int)self >= (int)Token.Type.LESS_THAN && (int)self <= (int)Token.Type.RETURN;

        public Tuple<float, float> binding_powers(){
            switch (self){
                case Token.Type.NOT:
                    return new(6.1f, 6.0f);
                case Token.Type.ASTERISK:
                case Token.Type.SLASH:
                case Token.Type.PERCENT:
                    return new(5.0f, 5.1f);
                case Token.Type.PLUS:
                case Token.Type.MINUS:
                    return new(4.0f, 4.1f);
                case Token.Type.LESS_THAN:
                case Token.Type.LESS_THAN_EQ:
                case Token.Type.GREATER_THAN:
                case Token.Type.GREATER_THAN_EQ:
                case Token.Type.EQUALS:
                case Token.Type.NOT_EQUALS:
                    return new(3.0f, 3.1f);
                case Token.Type.AND:
                case Token.Type.OR:
                    return new(2.0f, 2.1f);
                case Token.Type.EQ:
                    return new(1.1f, 1.0f);
                default:
                    throw new Exception($"<{self}> of type <Token.Type> does not have a binding power".colour_str());
            }
        }
    }
}

public sealed class Node{
    public Token token;
    public List<Node> sub_nodes = new();

    void tostring_helper_recurse(ref string s, int indent = 0){
        s += (new string(' ', indent) + token.id + '\n');

        foreach (Node n in sub_nodes)
            n.tostring_helper_recurse(ref s, indent + 4);
    }
    string tostring_helper(){
        string s = string.Empty;

        tostring_helper_recurse(ref s);

        s += (new string('-', 20) + '\n');

        return s;
    }

    public override string ToString() => tostring_helper();
}

public sealed class Parser{
    List<Node> m_nodes;

    public Parser(List<Token> tokens){
        m_nodes = new(); 

        if (tokens.Count((t) => t.type == Token.Type.LPAREN) != tokens.Count((t) => t.type == Token.Type.RPAREN))
            throw new Exception("The number of opening and closing parenthesis' must match".colour_str());
        if (tokens.Count((t) => t.type == Token.Type.LBRACE) != tokens.Count((t) => t.type == Token.Type.RBRACE))
            throw new Exception("The number of opening and closing braces must match".colour_str());

        // tokens.RemoveRange(0, 19);
        // tokens.RemoveRange(0, 5);
        // System.Console.WriteLine(parse_arithm_expr(tokens, 0.0f));
        System.Console.WriteLine(parse_expr(tokens));
        System.Console.WriteLine(parse_expr(tokens));
    }
    public Parser(Lexer lexer) : this(lexer.get_tokens()){}

    Node parse_arithm_expr(List<Token> tokens, float min_rhs_binding_power){
        Token tok = tokens[0];
        tokens.RemoveAt(0);

        Node lhs = new();

        if (tok.type.is_atom())
            lhs = new(){token = tok};
        else if (tok.type == Token.Type.LPAREN){
            Node temp = parse_arithm_expr(tokens, 0.0f);

            if (tokens[0].type != Token.Type.RPAREN)
                throw new Exception($"On line <{tokens[0].line_number}> expected <)>".colour_str());
            tokens.RemoveAt(0);

            lhs = temp;
        }
        else if (tok.type == Token.Type.PLUS || tok.type == Token.Type.MINUS){
            switch (tokens[0].type){
                case Token.Type.ID:
                case Token.Type.FALSE:
                case Token.Type.TRUE:
                case Token.Type.INT_LIT:
                case Token.Type.FLOAT_LIT:
                    lhs = new(){token = tok, sub_nodes = [new(){token = tokens[0]}]};;
                    tokens.RemoveAt(0);
                    break;
                case Token.Type.LPAREN:
                    return new(){token = tok, sub_nodes = [parse_arithm_expr(tokens, 0.0f)]};
                default:
                    throw new Exception($"On line <{tok.line_number}> found invalid token <{tok.id}>".colour_str());
            }
        }
        else
            throw new Exception($"On line <{tokens[0].line_number}> found invalid token <{tokens[0].id}>".colour_str());

        Token op;
        while (true){
            op = tokens[0];
            if (op.type == Token.Type.RPAREN || op.type == Token.Type.SEMICOLON)
                break;
            else if (!op.type.is_operation())
                throw new Exception($"On line <{tokens[0].line_number}> found invalid token <{tokens[0].id}>".colour_str());

            (float l_bp, float r_bp) = op.type.binding_powers();
            if (l_bp < min_rhs_binding_power)
                break;
            tokens.RemoveAt(0);

            Node rhs = parse_arithm_expr(tokens, r_bp);
            lhs = new(){token = op, sub_nodes = [lhs, rhs]};
        }

        return lhs;
    }
    Node parse_expr(List<Token> tokens){
        Node node = new();

        switch (tokens[0].type){
            case Token.Type.STR_LIT:
                break;

            case Token.Type.LBRACE:
                break;
            case Token.Type.RBRACE:
                break;

            case Token.Type.LET_DECL:
                node.token = new(){type = Token.Type.LET_DECL, id = "let"};
                tokens.RemoveAt(0);

                if (tokens[0].type != Token.Type.ID)
                    throw new Exception($"On line <{tokens[0].line_number}> <let> must be followed by an identifier".colour_str());

                node.sub_nodes.Add(new(){token = tokens[0]});
                tokens.RemoveAt(0);

                if (tokens[0].type != Token.Type.COLON)
                    throw new Exception($"On line <{tokens[0].line_number}> <{tokens[0].id}> must be followed by <:>".colour_str());

                tokens.RemoveAt(0);

                if (
                    tokens[0].type != Token.Type.BOOL &&
                    tokens[0].type != Token.Type.INT &&
                    tokens[0].type != Token.Type.FLOAT
                )
                    throw new Exception($"On line <{tokens[0].line_number}> <:> must be followed by a valid type".colour_str());

                node.sub_nodes.Add(new(){token = tokens[0]});
                tokens.RemoveAt(0);

                if (tokens[0].type != Token.Type.EQ)
                    throw new Exception($"On line <{tokens[0].line_number}> type must be followed by <=>".colour_str());
                tokens.RemoveAt(0);

                node.sub_nodes[1].sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));
                tokens.RemoveAt(0);

                break;

            case Token.Type.PRINT:
                break;
            case Token.Type.SCAN:
                break;

            case Token.Type.IF:
                break;
            case Token.Type.ELSE:
                break;

            case Token.Type.WHILE:
                break;

            case Token.Type.RETURN:
                break;

            default:
                throw new Exception($"On line <{tokens[0].line_number}> found invalid token <{tokens[0].id}>".colour_str());
        }

        return node;
    }

    /*
    -----------------------------------------------------------------------
                                    let
                                    / \
                                   /   \
                                  /     \
                                 a      int
                                        /
                                       -
                                      /
                                     *
                                    / \
                                   /   \
                                  /     \
                                 /       \
                                 +        -
                                / \      /
                              10   2    4
    -----------------------------------------------------------------------
                                    let
                                    / \
                                   /   \
                                  /     \
                                 b    float
                                        /
                                       +
                                      / \
                                     /   \
                                    /     \
                                   /       \
                                  -         \
                                 /           \
                               3.14           *
                                             / \
                                            /   \
                                           -     +
                                          /     / \
                                         a     /   \
                                              a     -
                                                   /
                                                  a
    -----------------------------------------------------------------------
                                     print
                                      /
                                     a
    -----------------------------------------------------------------------
                                      let
                                      /  \
                                    c44  int
                                         /
                                       scan
                                        /
                                    "Input: "
    -----------------------------------------------------------------------
                                       if
                                      /  \
                                     /    \
                                    /      \
                                   /        \
                                  >=        [0]
                                 /  \        |
                                /    \     return
                               /      \     /
                              /        \   c
                             c         10
    -----------------------------------------------------------------------
                                     else
                                      |
                                     [0]
                                      |
                                    while
                                    /   \
                                   /     \
                                  /       \
                                 /         \
                               and         [0]-----[1]
                               / \          |       |
                              /   \       print     =
                             /     \       /       / \
                            /       \     c       c   +
                           /         \               / \
                          /           \             c   1
                         /             \        
                        /               \               
                       <                or               
                      / \              /  \
                     c  10            /    \
                                     /      \
                                    >=       and
                                   / \      /   \
                                  c   0   false true
    -----------------------------------------------------------------------
    */
}
