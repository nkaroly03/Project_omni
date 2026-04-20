namespace Parse;

using Lex;

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

static class Parse_extensions{
    extension(Token.Type self){
        public static (float, float) BINDING_POWERS_UNARY => (11.1f, 11.0f);

        public bool is_atom() => (int)self >= (int)Token.Type.ID && (int)self <= (int)Token.Type.STR_LIT;
        public bool is_operation() => (int)self >= (int)Token.Type.EQUALS && (int)self <= (int)Token.Type.EQ;

        public (float, float) binding_powers() => self switch{
            Token.Type.EXP                                                => (12.1f, 12.0f),
            Token.Type.NOT or Token.Type.BITWISE_NEG                      => Token.Type.BINDING_POWERS_UNARY,
            Token.Type.ASTERISK or Token.Type.SLASH or Token.Type.PERCENT => (10.1f, 10.0f),
            Token.Type.PLUS or Token.Type.MINUS                           => ( 9.0f,  9.1f),
            Token.Type.SHIFT_LEFT or Token.Type.SHIFT_RIGHT               => ( 8.0f,  8.1f),

            Token.Type.EQUALS       or Token.Type.NOT_EQUALS      or
            Token.Type.LESS_THAN    or Token.Type.LESS_THAN_EQ    or
            Token.Type.GREATER_THAN or Token.Type.GREATER_THAN_EQ         => (7.0f, 7.1f),

            Token.Type.BITWISE_AND                                        => (6.0f, 6.1f),
            Token.Type.XOR                                                => (5.0f, 5.1f),
            Token.Type.BITWISE_OR                                         => (4.0f, 4.1f),
            Token.Type.AND                                                => (3.0f, 3.1f),
            Token.Type.OR                                                 => (2.0f, 2.1f),
            Token.Type.EQ                                                 => (1.1f, 1.0f),

            _ => throw new ArgumentOutOfRangeException($"<{self}> of type <Token.Type> does not have an associated binding power"),
        };
    }

    extension(Stack<Token> self){
        Node parse_arithm_expr(float prev_rbp){
            Node lhs = new();

            if (self.Count == 0)
                throw new Syntax_error_exception("No tokens are available");

            Token tok = self.Pop();
            if (tok.type.is_atom())
                lhs.token = tok;
            else if (tok.type == Token.Type.SCAN){
                lhs.token = tok;

                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <scan> must be followed by <(>");

                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.STR_LIT)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <scan> must contain a string literal");

                lhs.m_sub_nodes.Add(new(){token = tok});

                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <scan> must be closed by <)>");
            }
            else if (tok.type == Token.Type.ARGV){
                lhs.token = tok;

                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <argv> must be followed by <(>");

                lhs.m_sub_nodes.Add(self.parse_arithm_expr(0.0f));

                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <argv> must be closed by <)>");
            }
            else if (tok.type == Token.Type.LPAREN){
                lhs = self.parse_arithm_expr(0.0f);

                tok = self.Pop();
                if (tok.type != Token.Type.RPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> expected <)>");
            }
            else if (tok.type == Token.Type.PLUS || tok.type == Token.Type.MINUS || tok.type == Token.Type.BITWISE_NEG || tok.type == Token.Type.NOT){
                if (self.Count == 0)
                    throw new Syntax_error_exception($"On line {tok.line_number} no tokens are available");

                lhs = new(){token = tok, m_sub_nodes = [self.parse_arithm_expr(Token.Type.BINDING_POWERS_UNARY.Item2)]};
            }
            else
                throw new Syntax_error_exception($"On line <{self.Peek().line_number}> found invalid token <{self.Peek().id}>");

            while (true){
                if (self.Count == 0)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> no tokens are available");
                Token op = self.Peek();
                if (op.type == Token.Type.RPAREN || op.type == Token.Type.LBRACE || op.type == Token.Type.SEMICOLON)
                    break;
                else if (!op.type.is_operation())
                    throw new Syntax_error_exception($"On line <{op.line_number}> found invalid token <{op.id}>");

                (float lbp, float rbp) = op.type.binding_powers();
                if (lbp < prev_rbp)
                    break;
                self.Pop();

                Node rhs = self.parse_arithm_expr(rbp);
                lhs = new(){token = op, m_sub_nodes = [lhs, rhs]};
            }

            return lhs;
        }

        public (Node, Node?) parse_expr(){
            (Node node1, Node? node2) = (new(), null);

            Token tok = self.Pop();
            node1.token = tok;
            switch (tok.type){
                case Token.Type.ID:
                    if (self.Count == 0 || self.Peek().type != Token.Type.EQ)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <{tok.id}> must be followed by <=>");

                    self.Push(tok);

                    node1 = self.parse_arithm_expr(0.0f);

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.SEMICOLON)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <id> expression must be closed by <;>");
                    break;

                case Token.Type.LET_DECL:
                    if (self.Count == 0 || (tok = self.Peek()).type != Token.Type.ID)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <let> must be followed by an identifier");

                    node1.m_sub_nodes.Add(new(){token = self.Pop()});

                    if (self.Count == 0 || (tok = self.Peek()).type != Token.Type.COLON)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <{tok.id}> must be followed by <:>");

                    self.Pop();

                    if (self.Count == 0 || ((tok = self.Peek()).type != Token.Type.BOOL && tok.type != Token.Type.INT && tok.type != Token.Type.FLOAT))
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <:> must be followed by a valid type");

                    node1.m_sub_nodes.Add(new(){token = self.Pop()});

                    if (self.Count == 0 || (tok = self.Peek()).type != Token.Type.EQ)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> type must be followed by <=>");
                    self.Pop();

                    node1.m_sub_nodes[1].m_sub_nodes.Add(self.parse_arithm_expr(0.0f));
                    
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.SEMICOLON)
                        throw new Syntax_error_exception(($"On line <{tok.line_number}> let declaration must be closed by <;>"));
                    break;

                case Token.Type.PRINT:
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LPAREN)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <print> must be followed by <(>");

                    if (self.Count == 0 || (tok = self.Peek()).type == Token.Type.STR_LIT){
                        node1.m_sub_nodes.Add(new(){token = tok});
                        self.Pop();
                    }
                    else
                        node1.m_sub_nodes.Add(self.parse_arithm_expr(0.0f));

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RPAREN)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <print> must be closed by <)>");

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.SEMICOLON)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <print> statement must be close by <;>");
                    break;
                case Token.Type.SCAN:
                case Token.Type.ARGV:
                    throw new Syntax_error_exception($"On line <{tok.line_number}> discarding the result of <{tok.id}> statement is a bug");

                case Token.Type.IF:
                case Token.Type.WHILE:
                    bool is_while = (tok.type == Token.Type.WHILE);

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LPAREN)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> statement must start with <(>");
                    node1.m_sub_nodes.Add(self.parse_arithm_expr(0.0f));
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RPAREN)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> statement must end with <)>");

                    if (self.Count == 0)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> found missing token(s)");
                    tok = self.Peek();
                    if (tok.type == Token.Type.LBRACE){
                        self.Pop();
                        if (self.Count == 0)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> found missing token(s)");
                        while (self.Peek().type != Token.Type.RBRACE){
                            (Node n1, Node? n2) = self.parse_expr();
                            node1.m_sub_nodes.Add(n1);
                            if (n2 is not null)
                                node1.m_sub_nodes.Add(n2);
                        }
                        self.Pop();
                    }
                    else if (tok.type != Token.Type.SEMICOLON){
                        (Node n1, Node? n2) = self.parse_expr();
                        node1.m_sub_nodes.Add(n1);
                        if (n2 is not null)
                            node1.m_sub_nodes.Add(n2);
                    }
                    else
                        self.Pop();

                    if (self.Count > 0 && self.Peek().type == Token.Type.ELSE){
                        tok = self.Pop();
                        if (is_while)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> while statement is followed by else statement");

                        node2 = new(){token = tok};

                        if (self.Count == 0)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> found missing token(s)");
                        tok = self.Peek();
                        if (tok.type == Token.Type.LBRACE){
                            self.Pop();
                            if (self.Count == 0)
                                throw new Syntax_error_exception($"On line <{tok.line_number}> found missing token(s)");
                            while (self.Peek().type != Token.Type.RBRACE){
                                (Node n1, Node? n2) = self.parse_expr();
                                node2.m_sub_nodes.Add(n1);
                                if (n2 is not null)
                                    node2.m_sub_nodes.Add(n2);
                            }
                            self.Pop();
                        }
                        else if (tok.type != Token.Type.SEMICOLON){
                            (Node n1, Node? n2) = self.parse_expr();
                            node2.m_sub_nodes.Add(n1);
                            if (n2 is not null)
                                node2.m_sub_nodes.Add(n2);
                        }
                        else
                            self.Pop();
                    }
                    break;

                case Token.Type.RETURN:
                    node1.m_sub_nodes.Add(self.parse_arithm_expr(0.0f));
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.SEMICOLON)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <return> statement body must closed by <;>");
                    break;

                default:
                    throw new Syntax_error_exception($"On line <{tok.line_number}> found invalid token <{tok.id}>");
            }

            return (node1, node2);
        }
    }
}

public static class Parser{
    public static ReadOnlySpan<Node> build_AST(ReadOnlySpan<Token> tokens){
        Token[] token_array = tokens.ToArray();
        Array.Reverse(token_array);

        if (token_array.Count((t) => t.type == Token.Type.LPAREN) != token_array.Count((t) => t.type == Token.Type.RPAREN))
            throw new Syntax_error_exception("The number of opening and closing parentheses must match");
        if (token_array.Count((t) => t.type == Token.Type.LBRACE) != token_array.Count((t) => t.type == Token.Type.RBRACE))
            throw new Syntax_error_exception("The number of opening and closing braces must match");

        Stack<Token> token_stack = new(token_array);
        List<Node> nodes = new(); 

        while (((Func<bool>)(() => {
            while (token_stack.Count > 0 && token_stack.Peek().type == Token.Type.SEMICOLON)
                token_stack.Pop();
            return token_stack.Count > 0;
        }))()){
            (Node n1, Node? n2) = token_stack.parse_expr();
            nodes.Add(n1);
            if (n2 is not null)
                nodes.Add(n2);
        }

        if (nodes.Count == 0 || nodes[^1].token.type != Token.Type.RETURN)
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
