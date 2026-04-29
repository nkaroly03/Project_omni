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

public static class Parser{
    extension(Token.Type self){
        static (float, float) BINDING_POWERS_UNARY => (11.1f, 11.0f);

        bool is_atom() => (int)self >= (int)Token.Type.ID && (int)self <= (int)Token.Type.STR_LIT;
        bool is_operation() => (int)self >= (int)Token.Type.EQUALS && (int)self <= (int)Token.Type.EQ;

        (float, float) binding_powers() => self switch{
            Token.Type.ASTERISK2                                           => (12.1f, 12.0f),
            Token.Type.NOT or Token.Type.TILDE                             => Token.Type.BINDING_POWERS_UNARY,
            Token.Type.ASTERISK1 or Token.Type.SLASH or Token.Type.PERCENT => (10.0f, 10.1f),
            Token.Type.PLUS or Token.Type.MINUS                            => ( 9.0f,  9.1f),
            Token.Type.SHIFT_LEFT or Token.Type.SHIFT_RIGHT                => ( 8.0f,  8.1f),

            Token.Type.EQUALS       or Token.Type.NOT_EQUALS      or
            Token.Type.LESS_THAN    or Token.Type.LESS_THAN_EQ    or
            Token.Type.GREATER_THAN or Token.Type.GREATER_THAN_EQ          => (7.0f, 7.1f),

            Token.Type.AMPERSAND                                           => (6.0f, 6.1f),
            Token.Type.CARET                                               => (5.0f, 5.1f),
            Token.Type.PIPE                                                => (4.0f, 4.1f),
            Token.Type.AND                                                 => (3.0f, 3.1f),
            Token.Type.OR                                                  => (2.0f, 2.1f),
            Token.Type.EQ                                                  => (1.1f, 1.0f),

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
            else if (tok.type == Token.Type.SCAN || tok.type == Token.Type.ARRAY_SIZE){
                lhs.token = tok;

                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <{lhs.token.id}> must be followed by <(>");

                lhs.m_sub_nodes.Add(self.parse_arithm_expr(0.0f));

                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <{lhs.token.id}> must be closed by <)>");
            }
            else if (tok.type == Token.Type.RAND || tok.type == Token.Type.POLL_CHAR){
                lhs.token = tok;

                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <{lhs.token.id}> must be followed by <(>");
                if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> <{lhs.token.id}> must be closed by <)>");
            }
            else if (tok.type == Token.Type.LPAREN){
                lhs = self.parse_arithm_expr(0.0f);

                tok = self.Pop();
                if (tok.type != Token.Type.RPAREN)
                    throw new Syntax_error_exception($"On line <{tok.line_number}> expected <)>");
            }
            else if (tok.type == Token.Type.PLUS || tok.type == Token.Type.MINUS || tok.type == Token.Type.TILDE || tok.type == Token.Type.NOT){
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
                if (op.type == Token.Type.SEMICOLON || op.type == Token.Type.DOT2 || op.type == Token.Type.RPAREN || op.type == Token.Type.RBRACKET || op.type == Token.Type.LBRACE)
                    break;
                else if (op.type == Token.Type.LBRACKET){
                    self.Pop();
                    lhs = new(){token = op, m_sub_nodes = [lhs, self.parse_arithm_expr(0.0f)]};
                    if ((op = self.Pop()).type != Token.Type.RBRACKET)
                        throw new Syntax_error_exception($"On line <{op.line_number}> <[> must be closed by <]>");
                    continue;
                }
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

        (Node, Node?) parse_expr(bool loop_was_parsed){
            (Node node1, Node? node2) = (new(), null);

            Token tok = self.Pop();
            node1.token = tok;

            switch (tok.type){
                case Token.Type.ID:
                    if (self.Count == 0 || (self.Peek().type != Token.Type.EQ && self.Peek().type != Token.Type.LBRACKET))
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <{tok.id}> must be followed by <=>");

                    self.Push(tok);

                    node1 = self.parse_arithm_expr(0.0f);
                    if (node1.token.type == Token.Type.LBRACKET)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> discarding the result of array dereference");

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.SEMICOLON)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> expression must be closed by <;>");
                    break;

                case Token.Type.LBRACE:
                    if (self.Count == 0)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> found missing token(s)");
                    while (self.Peek().type != Token.Type.RBRACE){
                        (Node n1, Node? n2) = self.parse_expr(false);
                        node1.m_sub_nodes.Add(n1);
                        if (n2 is not null)
                            node1.m_sub_nodes.Add(n2);
                    }
                    self.Pop();
                    break;

                case Token.Type.LET_DECL:
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.ID)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <let> must be followed by an identifier");

                    node1.m_sub_nodes.Add(new(){token = tok});

                    if (self.Count == 0 || self.Peek().type != Token.Type.COLON)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <{tok.id}> must be followed by <:>");
                    self.Pop();

                    if (
                        self.Count == 0 ||
                        (
                            (tok = self.Pop()).type != Token.Type.LBRACKET && tok.type != Token.Type.BOOL && tok.type != Token.Type.CHAR &&
                            tok.type != Token.Type.INT && tok.type != Token.Type.FLOAT && tok.type != Token.Type.STR
                        )
                    )
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <:> must be followed by a valid type or <[>");

                    node1.m_sub_nodes.Add(new(){token = tok});

                    if (tok.type != Token.Type.LBRACKET){
                        if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.EQ)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> type must be followed by <=>");

                        node1.m_sub_nodes[1].m_sub_nodes.Add(self.parse_arithm_expr(0.0f));
                    }
                    else{
                        node1.m_sub_nodes[1].m_sub_nodes.Add(self.parse_arithm_expr(0.0f));
                        if ((tok = self.Pop()).type != Token.Type.RBRACKET)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> <[> must be closed by <]>");

                        if (
                            self.Count == 0 ||
                            (
                                (tok = self.Pop()).type != Token.Type.BOOL && tok.type != Token.Type.CHAR &&
                                tok.type != Token.Type.INT && tok.type != Token.Type.FLOAT && tok.type != Token.Type.STR
                            )
                        )
                            throw new Syntax_error_exception($"On line <{tok.line_number}> <]> must be followed by a valid type");

                        node1.m_sub_nodes[1].m_sub_nodes.Add(new(){token = tok});

                        if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.EQ)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> type must be followed by <=>");
                        if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LBRACE)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> <=> must be followed by <{{>");
                        if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RBRACE)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> <{{> must be followed by <}}>");
                    }
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.SEMICOLON)
                        throw new Syntax_error_exception(($"On line <{tok.line_number}> let declaration must be closed by <;>"));
                    break;

                case Token.Type.PRINT:
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LPAREN)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <print> must be followed by <(>");

                    node1.m_sub_nodes.Add(self.parse_arithm_expr(0.0f));

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RPAREN)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <print> must be closed by <)>");

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.SEMICOLON)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <print> statement must be close by <;>");
                    break;

                case Token.Type.SCAN:
                case Token.Type.ARGV:
                case Token.Type.ARRAY_SIZE:
                case Token.Type.RAND:
                case Token.Type.POLL_CHAR:
                    throw new Syntax_error_exception($"On line <{tok.line_number}> discarding the result of <{tok.id}> or trying to assign to it is a bug");

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
                    if (tok.type != Token.Type.SEMICOLON){
                        (Node n1, Node? n2) = self.parse_expr(is_while);
                        node1.m_sub_nodes.Add(n1);
                        if (n2 is not null)
                            node1.m_sub_nodes.Add(n2);
                    }
                    else
                        self.Pop();

                    if (!loop_was_parsed && self.Count > 0 && self.Peek().type == Token.Type.ELSE){
                        tok = self.Pop();
                        if (is_while)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> while statement is followed by else statement");

                        node2 = new(){token = tok};

                        if (self.Count == 0)
                            throw new Syntax_error_exception($"On line <{tok.line_number}> found missing token(s)");

                        tok = self.Peek();
                        if (tok.type != Token.Type.SEMICOLON){
                            (Node n1, Node? n2) = self.parse_expr(false);
                            node2.m_sub_nodes.Add(n1);
                            if (n2 is not null)
                                node2.m_sub_nodes.Add(n2);
                        }
                        else
                            self.Pop();
                    }
                    break;

                case Token.Type.FOR:
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.LPAREN)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <for> must be followed by <(>");

                    Node for_start_idx_node = self.parse_arithm_expr(0.0f);

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.DOT2)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <for>'s starting range must be followed by <..>");

                    Node for_end_idx_node = self.parse_arithm_expr(0.0f);

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.RPAREN)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <for>'s ending range must be closed by <)>");
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.PIPE)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <for>'s range expression must followed by <|>");
                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.ID)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> <|> must be followed by an identifier");

                    Token for_idx_id = tok;

                    if (self.Count == 0 || (tok = self.Pop()).type != Token.Type.PIPE)
                        throw new Syntax_error_exception($"On line <{for_idx_id.line_number}> <{for_idx_id.id}> must be followed by <|>");

                    if (self.Count == 0)
                        throw new Syntax_error_exception($"On line <{tok.line_number}> found missing token(s)");
                    
                    Node for_let_decl = new(){
                        token = new(){type = Token.Type.LET_DECL, id = "let"},
                        m_sub_nodes = [new(){token = for_idx_id}, new(){token = new(){type = Token.Type.INT, id = "int"}, m_sub_nodes = [for_start_idx_node]}]
                    };

                    Node for_condition = new(){token = new(){type = Token.Type.LESS_THAN, id = "<"}, m_sub_nodes = [new(){token = for_idx_id}, for_end_idx_node]};

                    Node for_block = new(){token = new(){type = Token.Type.LBRACE, id = "{"}};

                    tok = self.Peek();
                    if (tok.type != Token.Type.SEMICOLON){
                        (Node n1, Node? n2) = self.parse_expr(true);
                        for_block.m_sub_nodes.Add(n1);
                        if (n2 is not null)
                            for_block.m_sub_nodes.Add(n2);
                    }
                    else
                        self.Pop();

                    for_block.m_sub_nodes.Add(new(){
                        token = new(){type = Token.Type.EQ, id = "="},
                        m_sub_nodes = [
                            new(){token = for_idx_id},
                            new(){
                                token = new(){type = Token.Type.PLUS, id = "+"},
                                m_sub_nodes = [new(){token = for_idx_id}, new(){token = new(){type = Token.Type.INT_LIT, id = "1"}}]
                            }
                        ]
                    });

                    node1 = new(){
                        token = new(){type = Token.Type.LBRACE, id = "{"},
                        m_sub_nodes = [
                            for_let_decl,
                            new(){token = new(){type = Token.Type.WHILE, id = "while"}, m_sub_nodes = [for_condition, for_block]}
                        ]
                    };
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

    public static ReadOnlySpan<Node> build_AST(ReadOnlySpan<Token> tokens){
        Token[] token_array = tokens.ToArray();
        Array.Reverse(token_array);

        Stack<Token> token_stack = new(token_array);
        List<Node> nodes = new(); 

        while (((Func<bool>)(() => {
            while (token_stack.Count > 0 && token_stack.Peek().type == Token.Type.SEMICOLON)
                token_stack.Pop();
            return token_stack.Count > 0;
        }))()){
            (Node n1, Node? n2) = token_stack.parse_expr(false);
            nodes.Add(n1);
            if (n2 is not null)
                nodes.Add(n2);
        }

        if (nodes.Count == 0 || nodes[^1].token.type != Token.Type.RETURN)
            nodes.Add(new(){token = new(){type = Token.Type.RETURN, id = "return"}, m_sub_nodes = [new(){token = new(){type = Token.Type.INT_LIT, id = "0"}}]});

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(nodes);
    }

    /*
        the following ascii arts are not up to date
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
