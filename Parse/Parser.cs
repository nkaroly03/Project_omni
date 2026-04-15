namespace Parse;

using Lex;

static class Parse_extensions{
    extension(Token.Type self){
        public bool is_atom() => (int)self >= (int)Token.Type.ID && (int)self <= (int)Token.Type.STR_LIT;
        public bool is_operation() => (int)self >= (int)Token.Type.EQUALS && (int)self <= (int)Token.Type.EQ;

        public Tuple<float, float> binding_powers() => self switch{
            Token.Type.NOT or Token.Type.BITWISE_NEG                      => new(11.1f, 11.0f),
            Token.Type.ASTERISK or Token.Type.SLASH or Token.Type.PERCENT => new(10.1f, 10.0f),
            Token.Type.PLUS or Token.Type.MINUS                           => new( 9.0f,  9.1f),
            Token.Type.SHIFT_LEFT or Token.Type.SHIFT_RIGHT               => new( 8.0f,  8.1f),

            Token.Type.EQUALS       or Token.Type.NOT_EQUALS      or
            Token.Type.LESS_THAN    or Token.Type.LESS_THAN_EQ    or
            Token.Type.GREATER_THAN or Token.Type.GREATER_THAN_EQ         => new(7.0f, 7.1f),

            Token.Type.BITWISE_AND                                        => new(6.0f, 6.1f),
            Token.Type.XOR                                                => new(5.0f, 5.1f),
            Token.Type.BITWISE_OR                                         => new(4.0f, 4.1f),
            Token.Type.AND                                                => new(3.0f, 3.1f),
            Token.Type.OR                                                 => new(2.0f, 2.1f),
            Token.Type.EQ                                                 => new(1.1f, 1.0f),

            _ => throw new ArgumentOutOfRangeException($"<{self}> of type <Token.Type> does not have an associated binding power"),
        };
    }
}

public sealed class Node{
    internal List<Node> m_sub_nodes = new();

    public Token token{ get; internal set; }
    public ReadOnlySpan<Node> sub_nodes => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(m_sub_nodes);

    string tostring_helper(System.Text.StringBuilder sb, int indent = 0){
        sb.Append(' ', indent);
        sb.AppendLine(token.id);

        foreach (Node n in m_sub_nodes)
            n.tostring_helper(sb, indent + 4);

        return sb.ToString();
    }

    public override string ToString() => tostring_helper(new());
}

public static class Parser{
    static Node parse_arithm_expr(Stack<Token> tokens, float prev_rhs_binding_power){
        Node lhs = new();

        Token tok = tokens.Pop();
        if (tok.type.is_atom())
            lhs = new(){token = tok};
        else if (tok.type == Token.Type.SCAN){
            lhs.token = tok;

            tok = tokens.Pop();
            if (tok.type != Token.Type.LPAREN)
                throw new Syntax_error_exception($"On line <{tok.line_number}> <scan> must be followed by <(>");

            tok = tokens.Pop();
            if (tok.type != Token.Type.STR_LIT)
                throw new Syntax_error_exception($"On line <{tok.line_number}> <scan> must contain a string literal");

            lhs.m_sub_nodes.Add(new(){token = tok});

            tok = tokens.Pop();
            if (tok.type != Token.Type.RPAREN)
                throw new Syntax_error_exception($"On line <{tok.line_number}> <scan> must be closed by <)>");
        }
        else if (tok.type == Token.Type.LPAREN){
            Node temp = parse_arithm_expr(tokens, 0.0f);

            Token t = tokens.Pop();
            if (t.type != Token.Type.RPAREN)
                throw new Syntax_error_exception($"On line <{t.line_number}> expected <)>");

            lhs = temp;
        }
        else if (tok.type == Token.Type.PLUS || tok.type == Token.Type.MINUS || tok.type == Token.Type.BITWISE_NEG || tok.type == Token.Type.NOT){
            lhs = tokens.Peek().type switch{
                Token.Type.ID or Token.Type.FALSE or Token.Type.TRUE or Token.Type.INT_LIT or Token.Type.FLOAT_LIT
                    => new(){token = tok, m_sub_nodes = [new(){token = tokens.Pop()}]},

                Token.Type.LPAREN => new(){token = tok, m_sub_nodes = [parse_arithm_expr(tokens, Token.Type.NOT.binding_powers().Item2)]},

                _ => throw new Syntax_error_exception($"On line <{tok.line_number}> found invalid token <{tok.id}>"),
            };
        }
        else
            throw new Syntax_error_exception($"On line <{tokens.Peek().line_number}> found invalid token <{tokens.Peek().id}>");

        Token op;
        while (true){
            op = tokens.Peek();
            if (op.type == Token.Type.RPAREN || op.type == Token.Type.LBRACE || op.type == Token.Type.SEMICOLON)
                break;
            else if (!op.type.is_operation())
                throw new Syntax_error_exception($"On line <{op.line_number}> found invalid token <{op.id}>");

            (float l_bp, float r_bp) = op.type.binding_powers();
            if (l_bp < prev_rhs_binding_power)
                break;
            tokens.Pop();

            Node rhs = parse_arithm_expr(tokens, r_bp);
            lhs = new(){token = op, m_sub_nodes = [lhs, rhs]};
        }

        return lhs;
    }
    static Node parse_expr(Stack<Token> tokens){
        Node node = new();

        Token tok = tokens.Pop();
        node.token = tok;
        switch (tok.type){
            case Token.Type.ID:
                tokens.Push(tok);

                node = parse_arithm_expr(tokens, 0.0f);

                tok = tokens.Pop();
                if (tok.type != Token.Type.SEMICOLON)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <id> expression must be closed by <;>");

                break;

            case Token.Type.SEMICOLON:
                throw new Syntax_error_exception($"On line <{tok.line_number}> use of empty statement is a bug");

            case Token.Type.LET_DECL:
                tok = tokens.Peek();
                if (tok.type != Token.Type.ID)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <let> must be followed by an identifier");

                node.m_sub_nodes.Add(new(){token = tokens.Pop()});

                tok = tokens.Peek();
                if (tok.type != Token.Type.COLON)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <{tok.id}> must be followed by <:>");

                tokens.Pop();

                tok = tokens.Peek();
                if (tok.type != Token.Type.BOOL && tok.type != Token.Type.INT && tok.type != Token.Type.FLOAT)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <:> must be followed by a valid type");

                node.m_sub_nodes.Add(new(){token = tokens.Pop()});

                tok = tokens.Peek();
                if (tok.type != Token.Type.EQ)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> type must be followed by <=>");
                tok = tokens.Peek();

                if (tok.type == Token.Type.SCAN)
                    node.m_sub_nodes[1].m_sub_nodes.Add(parse_expr(tokens));
                else{
                    tokens.Pop();
                    node.m_sub_nodes[1].m_sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));
                }
                
                tok = tokens.Pop();

                break;

            case Token.Type.PRINT:
                tok = tokens.Pop();
                if (tok.type != Token.Type.LPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <print> must be followed by <(>");

                tok = tokens.Peek();
                if (tok.type == Token.Type.STR_LIT){
                    node.m_sub_nodes.Add(new(){token = tok});
                    tokens.Pop();
                }
                else
                    node.m_sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));

                tok = tokens.Pop();
                if (tok.type != Token.Type.RPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <print> must be closed by <)>");

                if (tokens.Count > 0){
                    tok = tokens.Pop();
                    if (tok.type != Token.Type.SEMICOLON)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <print> statement must be close by <;>");
                }
                else
                    throw new Syntax_error_exception($"On line <{tok.line_number}> found missing <;>");

                break;
            case Token.Type.SCAN:
                throw new Syntax_error_exception($"On line <{tok.line_number}> discarding the result of <scan> statement is a bug");

            case Token.Type.IF:
            case Token.Type.WHILE:
                node.m_sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));

                tok = tokens.Pop();
                if (tok.type != Token.Type.LBRACE)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <{tok.id}> statement body must be put inside <{{>");

                while (tokens.Peek().type != Token.Type.RBRACE)
                    node.m_sub_nodes.Add(parse_expr(tokens));

                tok = tokens.Pop();
                if (tok.type != Token.Type.RBRACE)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <{tok.id}> statement body must closed by <}}>");

                break;
            case Token.Type.ELSE:
                tok = tokens.Peek();

                if (tok.type == Token.Type.LBRACE){
                    tokens.Pop();

                    while (tokens.Peek().type != Token.Type.RBRACE)
                        node.m_sub_nodes.Add(parse_expr(tokens));

                    tok = tokens.Pop();
                    if (tok.type != Token.Type.RBRACE)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <else> statement body must closed by <}}>");
                }
                else if (tok.type == Token.Type.IF){
                    node.m_sub_nodes.Add(parse_expr(tokens));
                    if (tokens.Count > 0 && tokens.Peek().type == Token.Type.ELSE)
                        node.m_sub_nodes.Add(parse_expr(tokens));
                }
                else
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <else> statement must be preceded by <if>");

                break;

            case Token.Type.RETURN:
                node.m_sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));

                tok = tokens.Pop();
                if (tok.type != Token.Type.SEMICOLON)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <return> statement body must closed by <;>");

                break;

            default:
                throw new Syntax_error_exception($"On line <{tok.line_number}> found invalid token <{tok.id}>");
        }

        return node;
    }

    public static ReadOnlySpan<Node> build_AST(ReadOnlySpan<Token> tokens){
        List<Token> token_list = tokens.ToArray().ToList();
        token_list.Reverse();

        List<Node> nodes = new(); 

        Stack<Token> token_stack = new(token_list);

        if (token_list.Count((t) => t.type == Token.Type.LPAREN) != token_list.Count((t) => t.type == Token.Type.RPAREN))
            throw new Syntax_error_exception("The number of opening and closing parenthesis' must match");
        if (token_list.Count((t) => t.type == Token.Type.LBRACE) != token_list.Count((t) => t.type == Token.Type.RBRACE))
            throw new Syntax_error_exception("The number of opening and closing braces must match");

        while (token_stack.Count > 0)
            nodes.Add(parse_expr(token_stack));
        if (nodes.Last().token.type != Token.Type.RETURN)
            nodes.Add(new(){token = new(){type = Token.Type.RETURN, id = "return"}, m_sub_nodes = [new(){token = new(){type = Token.Type.INT_LIT, id = "0"}}]});

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(nodes);
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
