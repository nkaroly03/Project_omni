namespace Parse;

using Lex;

static class Parse_extensions{
    extension(Token.Type self){
        public bool is_atom() => (int)self >= (int)Token.Type.ID && (int)self <= (int)Token.Type.STR_LIT;
        public bool is_operation() => (int)self >= (int)Token.Type.LESS_THAN && (int)self <= (int)Token.Type.RETURN;

        public Tuple<float, float> binding_powers(){
            switch (self){
                case Token.Type.NOT:
                    return new(7.1f, 7.0f);
                case Token.Type.ASTERISK:
                case Token.Type.SLASH:
                case Token.Type.PERCENT:
                    return new(6.1f, 6.0f);
                case Token.Type.PLUS:
                case Token.Type.MINUS:
                    return new(5.0f, 5.1f);
                case Token.Type.LESS_THAN:
                case Token.Type.LESS_THAN_EQ:
                case Token.Type.GREATER_THAN:
                case Token.Type.GREATER_THAN_EQ:
                case Token.Type.EQUALS:
                case Token.Type.NOT_EQUALS:
                    return new(4.0f, 4.1f);
                case Token.Type.AND:
                    return new(3.0f, 3.1f);
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

public static class Parser{
    static Node parse_arithm_expr(Stack<Token> tokens, float min_rhs_binding_power){
        Node lhs = new();

        Token tok = tokens.Pop();
        if (tok.type.is_atom())
            lhs = new(){token = tok};
        else if (tok.type == Token.Type.SCAN){
            lhs.token = tok;

            tok = tokens.Pop();
            if (tok.type != Token.Type.LPAREN)
                throw new Exception($"On line <{tok.line_number}> <scan> must be followed by <(>".colour_str());

            tok = tokens.Pop();
            if (tok.type != Token.Type.STR_LIT)
                throw new Exception($"On line <{tok.line_number}> <scan> must contain a string literal".colour_str());

            lhs.sub_nodes.Add(new(){token = tok});

            tok = tokens.Pop();
            if (tok.type != Token.Type.RPAREN)
                throw new Exception($"On line <{tok.line_number}> <scan> must be closed by <)>".colour_str());
        }
        else if (tok.type == Token.Type.LPAREN){
            Node temp = parse_arithm_expr(tokens, 0.0f);

            Token t = tokens.Pop();
            if (t.type != Token.Type.RPAREN)
                throw new Exception($"On line <{t.line_number}> expected <)>".colour_str());

            lhs = temp;
        }
        else if (tok.type == Token.Type.NOT || tok.type == Token.Type.PLUS || tok.type == Token.Type.MINUS){
            switch (tokens.Peek().type){
                case Token.Type.ID:
                case Token.Type.FALSE:
                case Token.Type.TRUE:
                case Token.Type.INT_LIT:
                case Token.Type.FLOAT_LIT:
                    lhs = new(){token = tok, sub_nodes = [new(){token = tokens.Pop()}]};;
                    break;
                case Token.Type.LPAREN:
                    lhs = new(){token = tok, sub_nodes = [parse_arithm_expr(tokens, Token.Type.NOT.binding_powers().Item2)]};
                    break;
                default:
                    throw new Exception($"On line <{tok.line_number}> found invalid token <{tok.id}>".colour_str());
            }
        }
        else
            throw new Exception($"On line <{tokens.Peek().line_number}> found invalid token <{tokens.Peek().id}>".colour_str());

        Token op;
        while (true){
            op = tokens.Peek();
            if (op.type == Token.Type.RPAREN || op.type == Token.Type.LBRACE || op.type == Token.Type.SEMICOLON)
                break;
            else if (!op.type.is_operation())
                throw new Exception($"On line <{op.line_number}> found invalid token <{op.id}>".colour_str());

            (float l_bp, float r_bp) = op.type.binding_powers();
            if (l_bp < min_rhs_binding_power)
                break;
            tokens.Pop();

            Node rhs = parse_arithm_expr(tokens, r_bp);
            lhs = new(){token = op, sub_nodes = [lhs, rhs]};
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
                    throw new Exception($"On line <{tok.line_number}> <id> expression must be closed by <;>".colour_str());

                break;

            case Token.Type.SEMICOLON:
                throw new Exception($"On line <{tok.line_number}> use of empty statement is a bug".colour_str());

            case Token.Type.LET_DECL:
                tok = tokens.Peek();
                if (tok.type != Token.Type.ID)
                    throw new Exception($"On line <{tok.line_number}> <let> must be followed by an identifier".colour_str());

                node.sub_nodes.Add(new(){token = tokens.Pop()});

                tok = tokens.Peek();
                if (tok.type != Token.Type.COLON)
                    throw new Exception($"On line <{tok.line_number}> <{tok.id}> must be followed by <:>".colour_str());

                tokens.Pop();

                tok = tokens.Peek();
                if (tok.type != Token.Type.BOOL && tok.type != Token.Type.INT && tok.type != Token.Type.FLOAT)
                    throw new Exception($"On line <{tok.line_number}> <:> must be followed by a valid type".colour_str());

                node.sub_nodes.Add(new(){token = tokens.Pop()});

                tok = tokens.Peek();
                if (tok.type != Token.Type.EQ)
                    throw new Exception($"On line <{tok.line_number}> type must be followed by <=>".colour_str());
                tok = tokens.Peek();

                if (tok.type == Token.Type.SCAN)
                    node.sub_nodes[1].sub_nodes.Add(parse_expr(tokens));
                else{
                    tokens.Pop();
                    node.sub_nodes[1].sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));
                }
                
                tok = tokens.Pop();

                break;

            case Token.Type.PRINT:
                tok = tokens.Pop();
                if (tok.type != Token.Type.LPAREN)
                    throw new Exception($"On line <{tok.line_number}> <print> must be followed by <(>".colour_str());

                tok = tokens.Peek();
                if (tok.type == Token.Type.STR_LIT){
                    node.sub_nodes.Add(new(){token = tok});
                    tokens.Pop();
                }
                else
                    node.sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));

                tok = tokens.Pop();
                if (tok.type != Token.Type.RPAREN)
                    throw new Exception($"On line <{tok.line_number}> <print> must be closed by <)>".colour_str());

                tok = tokens.Pop();
                if (tok.type != Token.Type.SEMICOLON)
                    throw new Exception($"On line <{tok.line_number}> <print> statement must be close by <;>".colour_str());

                break;
            case Token.Type.SCAN:
                throw new Exception($"On line <{tok.line_number}> discarding the result of <scan> statement is a bug".colour_str());

            case Token.Type.IF:
            case Token.Type.WHILE:
                node.sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));

                tok = tokens.Pop();
                if (tok.type != Token.Type.LBRACE)
                    throw new Exception($"On line <{tok.line_number}> <{tok.id}> statement body must be put inside <{{>".colour_str());

                while (tokens.Peek().type != Token.Type.RBRACE)
                    node.sub_nodes.Add(parse_expr(tokens));

                tok = tokens.Pop();
                if (tok.type != Token.Type.RBRACE)
                    throw new Exception($"On line <{tok.line_number}> <{tok.id}> statement body must closed by <}}>".colour_str());

                break;
            case Token.Type.ELSE:
                tok = tokens.Peek();

                if (tok.type == Token.Type.LBRACE){
                    tokens.Pop();

                    while (tokens.Peek().type != Token.Type.RBRACE)
                        node.sub_nodes.Add(parse_expr(tokens));

                    tok = tokens.Pop();
                    if (tok.type != Token.Type.RBRACE)
                        throw new Exception($"On line <{tok.line_number}> <else> statement body must closed by <}}>".colour_str());
                }
                else if (tok.type == Token.Type.IF){
                    node.sub_nodes.Add(parse_expr(tokens));
                    if (tokens.Count > 0 && tokens.Peek().type == Token.Type.ELSE)
                        node.sub_nodes.Add(parse_expr(tokens));
                }
                else
                    throw new Exception($"On line <{tok.line_number}> <else> statement must be preceded by <if>".colour_str());

                break;

            case Token.Type.RETURN:
                node.sub_nodes.Add(parse_arithm_expr(tokens, 0.0f));

                tok = tokens.Pop();
                if (tok.type != Token.Type.SEMICOLON)
                    throw new Exception($"On line <{tok.line_number}> <return> statement body must closed by <;>".colour_str());

                break;

            default:
                throw new Exception($"On line <{tok.line_number}> found invalid token <{tok.id}>".colour_str());
        }

        return node;
    }

    public static List<Node> build_AST(List<Token> tokens){
        tokens.Reverse();

        List<Node> nodes = new(); 

        Stack<Token> token_stack = new(tokens);

        if (tokens.Count((t) => t.type == Token.Type.LPAREN) != tokens.Count((t) => t.type == Token.Type.RPAREN))
            throw new Exception("The number of opening and closing parenthesis' must match".colour_str());
        if (tokens.Count((t) => t.type == Token.Type.LBRACE) != tokens.Count((t) => t.type == Token.Type.RBRACE))
            throw new Exception("The number of opening and closing braces must match".colour_str());

        while (token_stack.Count > 0)
            nodes.Add(parse_expr(token_stack));
        if (nodes.Last().token.type != Token.Type.RETURN)
            nodes.Add(new(){token = new(){type = Token.Type.RETURN, id = "return"}, sub_nodes = [new(){token = new(){type = Token.Type.INT_LIT, id = "0"}}]});

        return nodes;
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
