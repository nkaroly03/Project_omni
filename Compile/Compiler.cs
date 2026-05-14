namespace Compile;

using Lex;
using Parse;
using Primitive;
using System.Diagnostics;
using System.Text;

using Type_info = Primitive.Type_info;
using Unary_op  = Primitive.Unary_op;
using Binary_op = Primitive.Binary_op;

public static class Compiler{
    extension(StringBuilder self){
        void add_instruction(string str) => self.AppendLine($"    {str}");
        int count_instructions() => self.ToString().Split(Environment.NewLine).Length;
    }
    extension(string self){
        char get_char_literal() => System.Text.RegularExpressions.Regex.Unescape(self[1..^1])[0];
        string get_string_literal() => System.Text.RegularExpressions.Regex.Unescape(self[1..^1]);
    }

    const string SP_SYMBOL    = "SP";
    const string PUSH_SYMBOL  = "PUSH";
    const string FALSE_SYMBOL = nameof(Token.type.FALSE);
    const string TRUE_SYMBOL  = nameof(Token.type.TRUE);
    const string ARGV_SYMBOL  = nameof(Token.type.ARGV);

    public enum Op_code : byte{
        PUSH_FROM_SP,
        PUSH_ARGV,
        PUSH_FALSE,
        PUSH_TRUE,
        PUSH_CHAR,
        PUSH_INT,
        PUSH_FLOAT,
        PUSH_STR,

        POP,

        RET,

        MOV,
        DEREF_MOV,

        JMP,
        JMPZ,

        PRINT,
        SCAN,

        ARRAY_SIZE,
        RAND,
        POLL_CHAR,

        TO_BOOL,
        TO_CHAR,
        TO_INT,
        TO_FLOAT,
        TO_STR,

        ALLOC_ARRAY,

        CMP_EQ,
        CMP_NEQ,
        CMP_LE,
        CMP_LEQ,
        CMP_GE,
        CMP_GEQ,

        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        POW,

        SHL,
        SHR,
        BAND,
        BOR,
        XOR,
        BNEG,

        DEREF,

        // AND,
        // OR,
        NEG
    }

    sealed class State{
        readonly record struct Id_info{
            public required readonly int stack_idx{ get; init; }
            public required readonly Type_info type_info{ get; init; }
        }

        static U comma_assignment_op<T, U>(ref T dest, T src, U ret){
            dest = src;
            return ret;
        }

        OrderedDictionary<string, Id_info> m_id_positions = new();
        List<int> m_let_decl_counts                       = [0];
        List<Type_info> m_type_info_stack;
        bool m_push_back_after_assignment                 = false;
        Type_info m_last_expr_type_info                   = Type_info.INVALID;
        public StringBuilder sb{ get; private set; }      = new();
        public int stack_size{ get; private set; }        = 0;

        void throw_on_invalid_unary_operation(Node node, Type_info type){
            if (m_last_expr_type_info == Type_info.INVALID)
                throw new Syntax_error_exception($"On line <{node.token.line_number}> invalid operation <{node.token.id}> on <{type.get_str_repr()}>");
        }
        void throw_on_invalid_binary_operation(Node node, Type_info lhs, Type_info rhs){
            if (m_last_expr_type_info == Type_info.INVALID)
                throw new Syntax_error_exception($"On line <{node.token.line_number}> invalid operation <{node.token.id}> between <{lhs.get_str_repr()}> and <{rhs.get_str_repr()}>");
        }
        void throw_on_invalid_binary_operation(int line_number, string op_str, Type_info lhs, Type_info rhs){
            if (m_last_expr_type_info == Type_info.INVALID)
                throw new Syntax_error_exception($"On line <{line_number}> invalid operation <{op_str}> between <{lhs.get_str_repr()}> and <{rhs.get_str_repr()}>");
        }

        public void to_IR(Node current_AST_node, Node? next_AST_node){
            bool current_push_back_after_assignment = m_push_back_after_assignment;
            StringBuilder current_sb = sb;
            m_push_back_after_assignment = true;

            Id_info id_info;
            Type_info lhs_type_info = default, rhs_type_info = default;

            switch (current_AST_node.token.type){
                case Token.Type.ID:
                    if (m_id_positions.TryGetValue(current_AST_node.token.id, out id_info)){
                        ++stack_size;
                        current_sb.add_instruction($"{stack_size} ; {PUSH_SYMBOL} {SP_SYMBOL}[-{stack_size - id_info.stack_idx - 1}]");
                        m_last_expr_type_info = id_info.type_info;
                    }
                    else
                        throw new Syntax_error_exception($"On line <{current_AST_node.token.line_number}> use of undeclared identifier <{current_AST_node.token.id}>");
                    break;
                case Token.Type.ARGV:
                    current_sb.add_instruction($"{++stack_size} ; {PUSH_SYMBOL} {ARGV_SYMBOL}");
                    m_last_expr_type_info = Type_info.ARRAY | Type_info.STR;
                    break;
                case Token.Type.FALSE:
                    current_sb.add_instruction($"{++stack_size} ; {PUSH_SYMBOL} {FALSE_SYMBOL}");
                    m_last_expr_type_info = Type_info.BOOL;
                    break;
                case Token.Type.TRUE:
                    current_sb.add_instruction($"{++stack_size} ; {PUSH_SYMBOL} {TRUE_SYMBOL}");
                    m_last_expr_type_info = Type_info.BOOL;
                    break;
                case Token.Type.CHAR_LIT:
                case Token.Type.INT_LIT:
                case Token.Type.FLOAT_LIT:
                case Token.Type.STR_LIT:
                    current_sb.add_instruction($"{++stack_size} ; {PUSH_SYMBOL} {current_AST_node.token.id}");
                    m_last_expr_type_info = current_AST_node.token.type switch{
                        Token.Type.CHAR_LIT  => Type_info.CHAR,
                        Token.Type.INT_LIT   => Type_info.INT,
                        Token.Type.FLOAT_LIT => Type_info.FLOAT,
                        Token.Type.STR_LIT   => Type_info.STR,

                        _ => throw new UnreachableException(),
                    };
                    break;

                case Token.Type.LBRACE:
                    m_push_back_after_assignment = false;

                    StringBuilder block_sb = sb = new();

                    m_let_decl_counts.Add(0);
                    if (current_AST_node.sub_nodes.Length > 0){
                        for (int i = 0; i < current_AST_node.sub_nodes.Length - 1; ++i)
                            to_IR(current_AST_node.sub_nodes[i], current_AST_node.sub_nodes[i + 1]);
                        to_IR(current_AST_node.sub_nodes[^1], null);
                    }

                    while (m_let_decl_counts[^1]-- > 0){
                        block_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.POP)}");
                        m_id_positions.RemoveAt(m_id_positions.Count - 1);
                    }
                    m_let_decl_counts.RemoveAt(m_let_decl_counts.Count - 1);
                    current_sb.Append(block_sb);
                    break;

                case Token.Type.EQUALS:
                case Token.Type.NOT_EQUALS:
                case Token.Type.LESS_THAN:
                case Token.Type.LESS_THAN_EQ:
                case Token.Type.GREATER_THAN:
                case Token.Type.GREATER_THAN_EQ:
                case Token.Type.ASTERISK1:
                case Token.Type.ASTERISK2:
                case Token.Type.SLASH:
                case Token.Type.PERCENT:
                case Token.Type.SHIFT_LEFT:
                case Token.Type.SHIFT_RIGHT:
                case Token.Type.AMPERSAND:
                case Token.Type.PIPE:
                case Token.Type.CARET:
                case Token.Type.LBRACKET:
                // case Token.Type.AND:
                // case Token.Type.OR:
                    Binary_op binary_op = default;
                    
                    to_IR(current_AST_node.sub_nodes[0], null);
                    lhs_type_info = m_last_expr_type_info;

                    to_IR(current_AST_node.sub_nodes[1], null);
                    rhs_type_info = m_last_expr_type_info;

                    current_sb.add_instruction($"{--stack_size} ; {current_AST_node.token.type switch{
                        Token.Type.EQUALS          => comma_assignment_op(ref binary_op, Binary_op.CMP_EQ,    nameof(Op_code.CMP_EQ)),
                        Token.Type.NOT_EQUALS      => comma_assignment_op(ref binary_op, Binary_op.CMP_NEQ,   nameof(Op_code.CMP_NEQ)),
                        Token.Type.LESS_THAN       => comma_assignment_op(ref binary_op, Binary_op.CMP_LE,    nameof(Op_code.CMP_LE)),
                        Token.Type.LESS_THAN_EQ    => comma_assignment_op(ref binary_op, Binary_op.CMP_LEQ,   nameof(Op_code.CMP_LEQ)),
                        Token.Type.GREATER_THAN    => comma_assignment_op(ref binary_op, Binary_op.CMP_GE,    nameof(Op_code.CMP_GE)),
                        Token.Type.GREATER_THAN_EQ => comma_assignment_op(ref binary_op, Binary_op.CMP_GEQ,   nameof(Op_code.CMP_GEQ)),
                        Token.Type.ASTERISK1       => comma_assignment_op(ref binary_op, Binary_op.MUL,       nameof(Op_code.MUL)),
                        Token.Type.ASTERISK2       => comma_assignment_op(ref binary_op, Binary_op.POW,       nameof(Op_code.POW)),
                        Token.Type.SLASH           => comma_assignment_op(ref binary_op, Binary_op.DIV,       nameof(Op_code.DIV)),
                        Token.Type.PERCENT         => comma_assignment_op(ref binary_op, Binary_op.MOD,       nameof(Op_code.MOD)),
                        Token.Type.SHIFT_LEFT      => comma_assignment_op(ref binary_op, Binary_op.SHL,       nameof(Op_code.SHL)),
                        Token.Type.SHIFT_RIGHT     => comma_assignment_op(ref binary_op, Binary_op.SHR,       nameof(Op_code.SHR)),
                        Token.Type.AMPERSAND       => comma_assignment_op(ref binary_op, Binary_op.BAND,      nameof(Op_code.BAND)),
                        Token.Type.PIPE            => comma_assignment_op(ref binary_op, Binary_op.BOR,       nameof(Op_code.BOR)),
                        Token.Type.CARET           => comma_assignment_op(ref binary_op, Binary_op.XOR,       nameof(Op_code.XOR)),
                        Token.Type.LBRACKET        => comma_assignment_op(ref binary_op, Binary_op.SUBSCRIPT, nameof(Op_code.DEREF)),
                        // Token.Type.AND             => "AND",
                        // Token.Type.OR              => "OR",
                        
                        _ => throw new UnreachableException(),
                    }}");

                    m_last_expr_type_info = binary_op.get_result_type(lhs_type_info, m_last_expr_type_info);
                    throw_on_invalid_binary_operation(current_AST_node, lhs_type_info, rhs_type_info);
                    break;

                case Token.Type.TILDE:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    lhs_type_info = m_last_expr_type_info;

                    current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.BNEG)}");

                    m_last_expr_type_info = Unary_op.BNEG.get_result_type(lhs_type_info);
                    throw_on_invalid_unary_operation(current_AST_node, lhs_type_info);
                    break;

                case Token.Type.AND:
                case Token.Type.OR:
                    StringBuilder lhs_and_or_sb = sb = new();
                    to_IR(current_AST_node.sub_nodes[0], null);

                    --stack_size;

                    StringBuilder rhs_and_or_sb = sb = new();
                    to_IR(current_AST_node.sub_nodes[1], null);

                    if (lhs_and_or_sb.ToString()[(lhs_and_or_sb.ToString().LastIndexOf(';') + 1)..].Trim() != nameof(Op_code.TO_BOOL))
                        lhs_and_or_sb.add_instruction($"{stack_size} ; {nameof(Op_code.TO_BOOL)}");
                    if (rhs_and_or_sb.ToString()[(rhs_and_or_sb.ToString().LastIndexOf(';') + 1)..].Trim() != nameof(Op_code.TO_BOOL))
                        rhs_and_or_sb.add_instruction($"{stack_size} ; {nameof(Op_code.TO_BOOL)}");

                    lhs_and_or_sb.add_instruction($"{++stack_size} ; {PUSH_SYMBOL} {SP_SYMBOL}[-1]");
                    if (current_AST_node.token.type == Token.Type.OR)
                        lhs_and_or_sb.add_instruction($"{stack_size} ; {nameof(Op_code.NEG)}");
                    lhs_and_or_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.JMPZ)} {rhs_and_or_sb.count_instructions()}");
                    lhs_and_or_sb.add_instruction($"{stack_size - 1} ; {nameof(Op_code.POP)}");

                    current_sb.Append(lhs_and_or_sb);
                    current_sb.Append(rhs_and_or_sb);
                    break;

                case Token.Type.NOT:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    lhs_type_info = m_last_expr_type_info;

                    current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.TO_BOOL)}");
                    current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.NEG)}");

                    m_last_expr_type_info = Unary_op.NOT.get_result_type(m_last_expr_type_info);
                    throw_on_invalid_unary_operation(current_AST_node, lhs_type_info);
                    break;

                case Token.Type.PLUS:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    lhs_type_info = m_last_expr_type_info;

                    if (current_AST_node.sub_nodes.Length > 1){
                        to_IR(current_AST_node.sub_nodes[1], null);
                        rhs_type_info = m_last_expr_type_info;

                        current_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.ADD)}");

                        m_last_expr_type_info = Binary_op.ADD.get_result_type(lhs_type_info, m_last_expr_type_info);
                        throw_on_invalid_binary_operation(current_AST_node, lhs_type_info, rhs_type_info);
                    }
                    else{
                        m_last_expr_type_info = Unary_op.PLUS.get_result_type(m_last_expr_type_info);
                        throw_on_invalid_unary_operation(current_AST_node, lhs_type_info);
                    }
                    break;
                case Token.Type.MINUS:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    lhs_type_info = m_last_expr_type_info;

                    if (current_AST_node.sub_nodes.Length > 1){
                        to_IR(current_AST_node.sub_nodes[1], null);
                        rhs_type_info = m_last_expr_type_info;

                        current_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.SUB)}");

                        m_last_expr_type_info = Binary_op.SUB.get_result_type(lhs_type_info, m_last_expr_type_info);
                        throw_on_invalid_binary_operation(current_AST_node, lhs_type_info, rhs_type_info);
                    }
                    else{
                        current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.NEG)}");

                        m_last_expr_type_info = Unary_op.MINUS.get_result_type(m_last_expr_type_info);
                        throw_on_invalid_unary_operation(current_AST_node, lhs_type_info);
                    }
                    break;

                case Token.Type.EQ:
                    Node eq_node_lhs = current_AST_node.sub_nodes[0];
                    if (eq_node_lhs.token.type != Token.Type.LBRACKET){
                        if (eq_node_lhs.token.type == Token.Type.ARGV)
                            throw new Syntax_error_exception($"On line <{eq_node_lhs.token.line_number}> <argv> is immutable");
                        if (eq_node_lhs.token.type != Token.Type.ID)
                            throw new Syntax_error_exception($"On line <{eq_node_lhs.token.line_number}> trying to assign to rvalue");
                        if (!m_id_positions.TryGetValue(eq_node_lhs.token.id, out id_info))
                            throw new Syntax_error_exception($"On line <{eq_node_lhs.token.line_number}> use of undeclared identifier <{eq_node_lhs.token.id}>");

                        to_IR(current_AST_node.sub_nodes[1], null);
                        lhs_type_info = id_info.type_info;
                        rhs_type_info = m_last_expr_type_info;

                        m_last_expr_type_info = Binary_op.ASSIGNMENT.get_result_type(lhs_type_info, rhs_type_info);
                        throw_on_invalid_binary_operation(current_AST_node, lhs_type_info, rhs_type_info);

                        current_sb.add_instruction($"{stack_size - 1} ; {nameof(Op_code.MOV)} {SP_SYMBOL}[-{stack_size - id_info.stack_idx}]");
                        --stack_size;

                        if (current_push_back_after_assignment){
                            to_IR(eq_node_lhs, null);
                            m_last_expr_type_info = lhs_type_info;
                        }
                        else
                            m_last_expr_type_info = Type_info.INVALID;
                    }
                    else{
                        for (Node lhs = current_AST_node.sub_nodes[0].sub_nodes[0]; lhs.token.type != Token.Type.ID; lhs = lhs.sub_nodes[0]){
                            if (lhs.token.type == Token.Type.ARGV)
                                throw new Syntax_error_exception($"On line <{lhs.token.line_number}> <argv> is immutable");
                            if (lhs.token.type != Token.Type.ID && lhs.token.type != Token.Type.LBRACKET && lhs.token.type != Token.Type.EQ)
                                throw new Syntax_error_exception($"On line <{lhs.token.line_number}> trying to assign to rvalue");
                        }

                        to_IR(current_AST_node.sub_nodes[0].sub_nodes[0], null);
                        lhs_type_info = m_last_expr_type_info;

                        to_IR(current_AST_node.sub_nodes[0].sub_nodes[1], null);
                        rhs_type_info = m_last_expr_type_info;

                        m_last_expr_type_info = Binary_op.SUBSCRIPT.get_result_type(lhs_type_info, rhs_type_info);
                        throw_on_invalid_binary_operation(current_AST_node.token.line_number, "[]", lhs_type_info, rhs_type_info);

                        if (current_push_back_after_assignment){
                            current_sb.add_instruction($"{++stack_size} ; {PUSH_SYMBOL} {SP_SYMBOL}[-2]");
                            current_sb.add_instruction($"{++stack_size} ; {PUSH_SYMBOL} {SP_SYMBOL}[-2]");
                        }

                        lhs_type_info = m_last_expr_type_info;
                        to_IR(current_AST_node.sub_nodes[1], null);
                        rhs_type_info = m_last_expr_type_info;

                        m_last_expr_type_info = Binary_op.ASSIGNMENT.get_result_type(lhs_type_info, rhs_type_info);
                        throw_on_invalid_binary_operation(current_AST_node, lhs_type_info, rhs_type_info);

                        stack_size -= 3;
                        current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.DEREF_MOV)}");
                        if (current_push_back_after_assignment)
                            current_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.DEREF)}");
                        else
                            m_last_expr_type_info = Type_info.INVALID;
                    }
                    break;

                case Token.Type.LET_DECL:
                    Type_info let_decl_type_info = Type_info.INVALID;
                    Node type_node = current_AST_node.sub_nodes[1];

                    to_IR(type_node.sub_nodes[0], null);
                    m_last_expr_type_info = Type_info.INVALID;

                    if (type_node.token.type == Token.Type.LBRACKET){
                        let_decl_type_info |= Type_info.ARRAY;
                        current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.ALLOC_ARRAY)}");
                        type_node = type_node.sub_nodes[1];
                    }
                    current_sb.add_instruction($"{stack_size} ; {type_node.token.type switch{
                        Token.Type.BOOL  => comma_assignment_op(ref let_decl_type_info, let_decl_type_info | Type_info.BOOL,  nameof(Op_code.TO_BOOL)),
                        Token.Type.CHAR  => comma_assignment_op(ref let_decl_type_info, let_decl_type_info | Type_info.CHAR,  nameof(Op_code.TO_CHAR)),
                        Token.Type.INT   => comma_assignment_op(ref let_decl_type_info, let_decl_type_info | Type_info.INT,   nameof(Op_code.TO_INT)),
                        Token.Type.FLOAT => comma_assignment_op(ref let_decl_type_info, let_decl_type_info | Type_info.FLOAT, nameof(Op_code.TO_FLOAT)),
                        Token.Type.STR   => comma_assignment_op(ref let_decl_type_info, let_decl_type_info | Type_info.STR,   nameof(Op_code.TO_STR)),

                        _ => throw new UnreachableException(),
                    }}"); 

                    Token let_decl_tok = current_AST_node.sub_nodes[0].token;
                    if (!m_id_positions.TryAdd(let_decl_tok.id, new(){stack_idx = m_id_positions.Count, type_info = let_decl_type_info}))
                        throw new Syntax_error_exception($"On line <{let_decl_tok.line_number}> identifier <{let_decl_tok.id}> is already in use");
                    ++m_let_decl_counts[^1];
                    break;

                case Token.Type.PRINT:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    current_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.PRINT)}");
                    m_last_expr_type_info = Type_info.INVALID;
                    break;
                case Token.Type.SCAN:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.SCAN)}");
                    m_last_expr_type_info = Type_info.STR;
                    break;

                case Token.Type.ARRAY_SIZE:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.ARRAY_SIZE)}");
                    if (!m_last_expr_type_info.array_is_set() && !m_last_expr_type_info.str_is_set())
                        throw new Syntax_error_exception($"On line <{current_AST_node.token.line_number}> trying to use <array_size> on non-array like expression");
                    m_last_expr_type_info = Type_info.INT;
                    break;
                case Token.Type.RAND:
                    current_sb.add_instruction($"{++stack_size} ; {nameof(Op_code.RAND)}");
                    m_last_expr_type_info = Type_info.INT;
                    break;
                case Token.Type.POLL_CHAR:
                    current_sb.add_instruction($"{++stack_size} ; {nameof(Op_code.POLL_CHAR)}");
                    m_last_expr_type_info = Type_info.CHAR;
                    break;

                case Token.Type.IF:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    if (m_last_expr_type_info.array_is_set() || m_last_expr_type_info.str_is_set())
                        throw new Syntax_error_exception($"On line <{current_AST_node.token.line_number}> trying to use <{m_last_expr_type_info.get_str_repr()}> as a bool");

                    m_push_back_after_assignment = false;
                    m_last_expr_type_info = Type_info.INVALID;
                    --stack_size;

                    StringBuilder if_else_sb = sb = new();
                    bool has_else_after = (next_AST_node is not null && next_AST_node.token.type == Token.Type.ELSE);

                    if (current_AST_node.sub_nodes.Length > 1){
                        m_let_decl_counts.Add(0);
                        to_IR(current_AST_node.sub_nodes[1], null);
                        while (m_let_decl_counts[^1]-- > 0){
                            if_else_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.POP)}");
                            m_id_positions.RemoveAt(m_id_positions.Count - 1);
                        }
                        m_let_decl_counts.RemoveAt(m_let_decl_counts.Count - 1);
                    }
                    current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.JMPZ)} {if_else_sb.count_instructions() + Convert.ToInt32(has_else_after)}");
                    current_sb.Append(if_else_sb);

                    if (has_else_after){
                        if_else_sb.Clear();

                        m_last_expr_type_info = Type_info.INVALID;

                        if (next_AST_node!.sub_nodes.Length > 0){
                            m_let_decl_counts.Add(0);
                            to_IR(next_AST_node.sub_nodes[0], (next_AST_node!.sub_nodes.Length > 1) ? next_AST_node!.sub_nodes[1] : null);
                            while (m_let_decl_counts[^1]-- > 0){
                                if_else_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.POP)}");
                                m_id_positions.RemoveAt(m_id_positions.Count - 1);
                            }
                            m_let_decl_counts.RemoveAt(m_let_decl_counts.Count - 1);
                        }
                        current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.JMP)} {if_else_sb.count_instructions()}");
                        current_sb.Append(if_else_sb);
                    }
                    break;
                case Token.Type.ELSE:
                    break;
                case Token.Type.WHILE:
                    StringBuilder while_condition_sb = sb = new();

                    to_IR(current_AST_node.sub_nodes[0], null);
                    if (m_last_expr_type_info.array_is_set() || m_last_expr_type_info.str_is_set())
                        throw new Syntax_error_exception($"On line <{current_AST_node.token.line_number}> trying to use <{m_last_expr_type_info.get_str_repr()}> as a bool");

                    m_push_back_after_assignment = false;
                    m_last_expr_type_info = Type_info.INVALID;
                    --stack_size;
                    
                    StringBuilder while_sb = sb = new();

                    if (current_AST_node.sub_nodes.Length > 1){
                        m_let_decl_counts.Add(0);
                        to_IR(current_AST_node.sub_nodes[1], null);
                        while (m_let_decl_counts[^1]-- > 0){
                            while_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.POP)}");
                            m_id_positions.RemoveAt(m_id_positions.Count - 1);
                        }
                        m_let_decl_counts.RemoveAt(m_let_decl_counts.Count - 1);
                    }

                    while_condition_sb.add_instruction($"{stack_size} ; {nameof(Op_code.JMPZ)} {while_sb.count_instructions() + 1}");
                    current_sb.Append(while_condition_sb);
                    current_sb.Append(while_sb);
                    current_sb.add_instruction($"{stack_size} ; {nameof(Op_code.JMP)} -{while_condition_sb.count_instructions() + while_sb.count_instructions() - 2}");
                    break;

                case Token.Type.RETURN:
                    to_IR(current_AST_node.sub_nodes[0], null);
                    current_sb.add_instruction($"{--stack_size} ; {nameof(Op_code.RET)}");
                    m_last_expr_type_info = Type_info.INVALID;
                    break;

                default:
                    throw new NotImplementedException($"Case for <{current_AST_node.token.type}> is not implemented");
            }

            sb = current_sb;
            m_push_back_after_assignment = current_push_back_after_assignment;
        }
    }

    public static string to_IR(ReadOnlySpan<Node> AST){
        State state = new();

        for (int i = 0; i < AST.Length - 1; ++i)
            state.to_IR(AST[i], AST[i + 1]);
        state.to_IR(AST[^1], null);

        for (int i = state.stack_size; i-- > 0;)
            state.sb.add_instruction($"{i} ; {nameof(Op_code.POP)}");

        return state.sb.ToString();
    }
    public static string to_IR(ReadOnlySpan<Token> tokens) => to_IR(Parser.build_AST(tokens));
    public static string to_IR(string path) => to_IR(Lexer.tokenize(path));

    static int count_bytes_in_instructions(ReadOnlySpan<string> instructions){
        int instruction_byte_count = 0;

        foreach (string instruction in instructions){
            instruction_byte_count += sizeof(Op_code);
            int space_idx = instruction.IndexOf(' ');
            (string lhs, string rhs) = (space_idx >= 0) ? (instruction[..space_idx], instruction[(space_idx + 1)..]) : (instruction, "");
            switch (lhs){
                case PUSH_SYMBOL:
                    if (rhs != ARGV_SYMBOL && rhs != FALSE_SYMBOL && rhs != TRUE_SYMBOL){
                        if (rhs.StartsWith('"'))
                            instruction_byte_count += (sizeof(int) + Encoding.UTF8.GetBytes(rhs.get_string_literal()).Length);
                        else{
                            instruction_byte_count += (
                                (rhs.StartsWith('\''))
                                    ? sizeof(char)
                                    : ((rhs.StartsWith(SP_SYMBOL) || rhs.IndexOf('.') < 0) ? sizeof(int) : sizeof(float))
                            );
                        }
                    }
                    break;
                case nameof(Op_code.MOV):
                case nameof(Op_code.JMP):
                case nameof(Op_code.JMPZ):
                    instruction_byte_count += sizeof(int);
                    break;
            }
        }

        return instruction_byte_count;
    }

    public static ReadOnlySpan<byte> to_bytecode(string IR){
        List<byte> bytecode = new();
        string[] instructions = IR.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((s) => s[(s.IndexOf(';') + 2)..]).ToArray();
        foreach ((int i, string instruction) in instructions.Index()){
            int space_idx = instruction.IndexOf(' ');
            (string lhs, string rhs) = (space_idx >= 0) ? (instruction[..space_idx], instruction[(space_idx + 1)..]) : (instruction, "");
            byte[] as_bytes;
            switch (lhs){
                case PUSH_SYMBOL:
                    if (rhs.StartsWith(SP_SYMBOL)){
                        as_bytes = BitConverter.GetBytes(int.Parse(rhs[3..^1]));
                        bytecode.Add((byte)Op_code.PUSH_FROM_SP);
                        bytecode.AddRange(as_bytes);
                    }
                    else if (rhs == ARGV_SYMBOL)
                        bytecode.Add((byte)Op_code.PUSH_ARGV);
                    else if (rhs == FALSE_SYMBOL)
                        bytecode.Add((byte)Op_code.PUSH_FALSE);
                    else if (rhs == TRUE_SYMBOL)
                        bytecode.Add((byte)Op_code.PUSH_TRUE);
                    else if (rhs.StartsWith('\'')){
                        as_bytes = BitConverter.GetBytes(rhs.get_char_literal());
                        bytecode.Add((byte)Op_code.PUSH_CHAR);
                        bytecode.AddRange(as_bytes);
                    }
                    else if (rhs.StartsWith('"')){
                        as_bytes = Encoding.UTF8.GetBytes(rhs.get_string_literal());
                        bytecode.Add((byte)Op_code.PUSH_STR);
                        bytecode.AddRange(BitConverter.GetBytes(as_bytes.Length));
                        bytecode.AddRange(as_bytes);
                    }
                    else if (rhs.IndexOf('.') < 0){
                        as_bytes = BitConverter.GetBytes(int.Parse(rhs));
                        bytecode.Add((byte)Op_code.PUSH_INT);
                        bytecode.AddRange(as_bytes);
                    }
                    else{
                        as_bytes = BitConverter.GetBytes(float.Parse(rhs, System.Globalization.CultureInfo.InvariantCulture));
                        bytecode.Add((byte)Op_code.PUSH_FLOAT);
                        bytecode.AddRange(as_bytes);
                    }
                    break;

                case nameof(Op_code.POP): bytecode.Add((byte)Op_code.POP); break;
                case nameof(Op_code.RET): bytecode.Add((byte)Op_code.RET); break;

                case nameof(Op_code.MOV):
                    as_bytes = BitConverter.GetBytes(int.Parse(rhs[3..^1]));
                    bytecode.Add((byte)Op_code.MOV);
                    bytecode.AddRange(as_bytes);
                    break;
                case nameof(Op_code.DEREF_MOV):
                    bytecode.Add((byte)Op_code.DEREF_MOV);
                    break;

                case nameof(Op_code.JMP):
                case nameof(Op_code.JMPZ):
                    int jmp_count = int.Parse(rhs);
                    int jmp_byte_count = (jmp_count >= 0)
                        ? count_bytes_in_instructions(instructions.AsSpan()[(i + 1)..][..(jmp_count - 1)]) - 1
                        : -(count_bytes_in_instructions(instructions.AsSpan()[(i + jmp_count)..][..(1 - jmp_count)]) + 1)
                    ;
                    as_bytes = BitConverter.GetBytes(jmp_byte_count);
                    bytecode.Add((lhs == nameof(Op_code.JMP)) ? (byte)Op_code.JMP : (byte)Op_code.JMPZ);
                    bytecode.AddRange(as_bytes);
                    break;

                case nameof(Op_code.PRINT):       bytecode.Add((byte)Op_code.PRINT);       break;
                case nameof(Op_code.SCAN):        bytecode.Add((byte)Op_code.SCAN);        break;
                case nameof(Op_code.ARRAY_SIZE):  bytecode.Add((byte)Op_code.ARRAY_SIZE);  break;
                case nameof(Op_code.RAND):        bytecode.Add((byte)Op_code.RAND);        break;
                case nameof(Op_code.POLL_CHAR):   bytecode.Add((byte)Op_code.POLL_CHAR);   break;
                case nameof(Op_code.TO_BOOL):     bytecode.Add((byte)Op_code.TO_BOOL);     break;
                case nameof(Op_code.TO_CHAR):     bytecode.Add((byte)Op_code.TO_CHAR);     break;
                case nameof(Op_code.TO_INT):      bytecode.Add((byte)Op_code.TO_INT);      break;
                case nameof(Op_code.TO_FLOAT):    bytecode.Add((byte)Op_code.TO_FLOAT);    break;
                case nameof(Op_code.TO_STR):      bytecode.Add((byte)Op_code.TO_STR);      break;
                case nameof(Op_code.ALLOC_ARRAY): bytecode.Add((byte)Op_code.ALLOC_ARRAY); break;
                case nameof(Op_code.CMP_EQ):      bytecode.Add((byte)Op_code.CMP_EQ);      break;
                case nameof(Op_code.CMP_NEQ):     bytecode.Add((byte)Op_code.CMP_NEQ);     break;
                case nameof(Op_code.CMP_LE):      bytecode.Add((byte)Op_code.CMP_LE);      break;
                case nameof(Op_code.CMP_LEQ):     bytecode.Add((byte)Op_code.CMP_LEQ);     break;
                case nameof(Op_code.CMP_GE):      bytecode.Add((byte)Op_code.CMP_GE);      break;
                case nameof(Op_code.CMP_GEQ):     bytecode.Add((byte)Op_code.CMP_GEQ);     break;
                case nameof(Op_code.ADD):         bytecode.Add((byte)Op_code.ADD);         break;
                case nameof(Op_code.SUB):         bytecode.Add((byte)Op_code.SUB);         break;
                case nameof(Op_code.MUL):         bytecode.Add((byte)Op_code.MUL);         break;
                case nameof(Op_code.DIV):         bytecode.Add((byte)Op_code.DIV);         break;
                case nameof(Op_code.MOD):         bytecode.Add((byte)Op_code.MOD);         break;
                case nameof(Op_code.POW):         bytecode.Add((byte)Op_code.POW);         break;
                case nameof(Op_code.SHL):         bytecode.Add((byte)Op_code.SHL);         break;
                case nameof(Op_code.SHR):         bytecode.Add((byte)Op_code.SHR);         break;
                case nameof(Op_code.BAND):        bytecode.Add((byte)Op_code.BAND);        break;
                case nameof(Op_code.BOR):         bytecode.Add((byte)Op_code.BOR);         break;
                case nameof(Op_code.XOR):         bytecode.Add((byte)Op_code.XOR);         break;
                case nameof(Op_code.BNEG):        bytecode.Add((byte)Op_code.BNEG);        break;
                case nameof(Op_code.DEREF):       bytecode.Add((byte)Op_code.DEREF);       break;
                // case "AND":         bytecode.Add((byte)Op_code.AND);         break;
                // case "OR":          bytecode.Add((byte)Op_code.OR);          break;
                case nameof(Op_code.NEG):         bytecode.Add((byte)Op_code.NEG);         break;

                default: throw new NotImplementedException($"Case for <{lhs}> is not implemented");
            }
        }

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bytecode);
    }
}
