namespace Compile;

using Lex;
using Parse;
using System.Text;

public static class Compiler{
    extension(StringBuilder self){
        void add_instruction(string str) => self.AppendLine($"    {str}");
        int count_instructions() => self.ToString().Split(Environment.NewLine).Length;
    }
    extension(string self){
        char get_char_literal() => System.Text.RegularExpressions.Regex.Unescape(self[1..^1])[0];
        string get_string_literal() => System.Text.RegularExpressions.Regex.Unescape(self[1..^1]);
    }

    const string SP_STR    = "SP";
    const string PUSH_STR  = "PUSH";
    const string FALSE_STR = nameof(Token.type.FALSE);
    const string TRUE_STR  = nameof(Token.type.TRUE);
    const string ARGV_STR  = nameof(Token.type.ARGV);

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
        NEG,
    }

    sealed class State{
        OrderedDictionary<string, int> m_id_positions = new();
        public int stack_size{ get; private set; }

        public void to_IR(Node current_AST_node, Node? next_AST_node, StringBuilder sb, ref int let_decl_counter, bool push_back_after_assignment){
            switch (current_AST_node.token.type){
                case Token.Type.ID:
                    if (!m_id_positions.ContainsKey(current_AST_node.token.id))
                        throw new Syntax_error_exception($"On line <{current_AST_node.token.line_number}> use of undeclared identifier <{current_AST_node.token.id}>");
                    ++stack_size;
                    sb.add_instruction($"{stack_size} ; {PUSH_STR} {SP_STR}[-{stack_size - m_id_positions[current_AST_node.token.id] - 1}]");
                    break;
                case Token.Type.ARGV:
                    sb.add_instruction($"{++stack_size} ; {PUSH_STR} {ARGV_STR}");
                    break;
                case Token.Type.FALSE:
                    sb.add_instruction($"{++stack_size} ; {PUSH_STR} {FALSE_STR}");
                    break;
                case Token.Type.TRUE:
                    sb.add_instruction($"{++stack_size} ; {PUSH_STR} {TRUE_STR}");
                    break;
                case Token.Type.CHAR_LIT:
                case Token.Type.INT_LIT:
                case Token.Type.FLOAT_LIT:
                case Token.Type.STR_LIT:
                    sb.add_instruction($"{++stack_size} ; {PUSH_STR} {current_AST_node.token.id}");
                    break;

                case Token.Type.LBRACE:
                    StringBuilder block_sb = new();
                    int block_let_decl_counter = 0;
                    if (current_AST_node.sub_nodes.Length > 0){
                        for (int i = 0; i < current_AST_node.sub_nodes.Length - 1; ++i)
                            to_IR(current_AST_node.sub_nodes[i], current_AST_node.sub_nodes[i + 1], block_sb, ref block_let_decl_counter, false);
                        to_IR(current_AST_node.sub_nodes[^1], null, block_sb, ref block_let_decl_counter, false);
                    }
                    while (block_let_decl_counter-- > 0){
                        block_sb.add_instruction($"{--stack_size} ; {Op_code.POP}");
                        m_id_positions.RemoveAt(m_id_positions.Count - 1);
                    }
                    sb.Append(block_sb);
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
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{--stack_size} ; {current_AST_node.token.type switch{
                        Token.Type.EQUALS          => Op_code.CMP_EQ,
                        Token.Type.NOT_EQUALS      => Op_code.CMP_NEQ,
                        Token.Type.LESS_THAN       => Op_code.CMP_LE,
                        Token.Type.LESS_THAN_EQ    => Op_code.CMP_LEQ,
                        Token.Type.GREATER_THAN    => Op_code.CMP_GE,
                        Token.Type.GREATER_THAN_EQ => Op_code.CMP_GEQ,
                        Token.Type.ASTERISK1       => Op_code.MUL,
                        Token.Type.ASTERISK2       => Op_code.POW,
                        Token.Type.SLASH           => Op_code.DIV,
                        Token.Type.PERCENT         => Op_code.MOD,
                        Token.Type.SHIFT_LEFT      => Op_code.SHL,
                        Token.Type.SHIFT_RIGHT     => Op_code.SHR,
                        Token.Type.AMPERSAND       => Op_code.BAND,
                        Token.Type.PIPE            => Op_code.BOR,
                        Token.Type.CARET           => Op_code.XOR,
                        Token.Type.LBRACKET        => Op_code.DEREF,
                        // Token.Type.AND             => "AND",
                        // Token.Type.OR              => "OR",
                        
                        _ => throw new System.Diagnostics.UnreachableException(),
                    }}");
                    break;

                case Token.Type.TILDE:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size} ; {Op_code.BNEG}");
                    break;

                case Token.Type.AND:
                case Token.Type.OR:
                    StringBuilder and_or_sb = new();
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    --stack_size;
                    to_IR(current_AST_node.sub_nodes[1], null, and_or_sb, ref let_decl_counter, true);
                    if (sb.ToString()[(sb.ToString().LastIndexOf(';') + 1)..].Trim() != nameof(Op_code.TO_BOOL))
                        sb.add_instruction($"{stack_size} ; {Op_code.TO_BOOL}");
                    if (current_AST_node.token.type == Token.Type.AND){
                        sb.add_instruction($"{stack_size} ; {Op_code.NEG}");
                        sb.add_instruction($"{stack_size - 1} ; {Op_code.JMPZ} 3");
                        sb.add_instruction($"{stack_size} ; {PUSH_STR} {FALSE_STR}");
                    }
                    else{
                        sb.add_instruction($"{stack_size - 1} ; {Op_code.JMPZ} 3");
                        sb.add_instruction($"{stack_size} ; {PUSH_STR} {TRUE_STR}");
                    }
                    sb.add_instruction($"{stack_size} ; {Op_code.JMP} {and_or_sb.count_instructions() + 1}");
                    sb.Append(and_or_sb);
                    if (sb.ToString()[(sb.ToString().LastIndexOf(';') + 1)..].Trim() != nameof(Op_code.TO_BOOL))
                        sb.add_instruction($"{stack_size} ; {Op_code.TO_BOOL}");
                    break;
                case Token.Type.NOT:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size} ; {Op_code.TO_BOOL}");
                    sb.add_instruction($"{stack_size} ; {Op_code.NEG}");
                    break;

                case Token.Type.PLUS:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    if (current_AST_node.sub_nodes.Length > 1){
                        to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                        sb.add_instruction($"{--stack_size} ; {Op_code.ADD}");
                    }
                    break;
                case Token.Type.MINUS:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    if (current_AST_node.sub_nodes.Length > 1){
                        to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                        sb.add_instruction($"{--stack_size} ; {Op_code.SUB}");
                    }
                    else
                        sb.add_instruction($"{stack_size} ; {Op_code.NEG}");
                    break;

                case Token.Type.EQ:
                    Token eq_tok = current_AST_node.sub_nodes[0].token;
                    if (eq_tok.type != Token.Type.LBRACKET){
                        if (eq_tok.type == Token.Type.ARGV)
                            throw new Syntax_error_exception($"On line <{eq_tok.line_number}> <argv> is immutable");
                        else if (eq_tok.type != Token.Type.ID)
                            throw new Syntax_error_exception($"On line <{eq_tok.line_number}> trying to assign to rvalue");
                        else if (!m_id_positions.ContainsKey(eq_tok.id))
                            throw new Syntax_error_exception($"On line <{eq_tok.line_number}> use of undeclared identifier <{eq_tok.id}>");

                        to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                        sb.add_instruction($"{stack_size - 1} ; {Op_code.MOV} {SP_STR}[-{stack_size - m_id_positions[eq_tok.id]}]");
                        --stack_size;

                        if (push_back_after_assignment)
                            to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    }
                    else{
                        for (Node lhs = current_AST_node.sub_nodes[0].sub_nodes[0]; lhs.token.type != Token.Type.ID; lhs = lhs.sub_nodes[0]){
                            if (lhs.token.type == Token.Type.ARGV)
                                throw new Syntax_error_exception($"On line <{lhs.token.line_number}> <argv> is immutable");
                            else if (lhs.token.type != Token.Type.ID && lhs.token.type != Token.Type.LBRACKET && lhs.token.type != Token.Type.EQ)
                                throw new Syntax_error_exception($"On line <{lhs.token.line_number}> trying to assign to rvalue");
                        }
                        to_IR(current_AST_node.sub_nodes[0].sub_nodes[0], null, sb, ref let_decl_counter, true);
                        to_IR(current_AST_node.sub_nodes[0].sub_nodes[1], null, sb, ref let_decl_counter, true);
                        if (push_back_after_assignment){
                            sb.add_instruction($"{++stack_size} ; {PUSH_STR} {SP_STR}[-2]");
                            sb.add_instruction($"{++stack_size} ; {PUSH_STR} {SP_STR}[-2]");
                        }
                        to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                        stack_size -= 3;
                        sb.add_instruction($"{stack_size} ; {Op_code.DEREF_MOV}");
                        if (push_back_after_assignment)
                            sb.add_instruction($"{--stack_size} ; {Op_code.DEREF}");
                    }
                    break;

                case Token.Type.LET_DECL:
                    to_IR(current_AST_node.sub_nodes[1].sub_nodes[0], null, sb, ref let_decl_counter, true);
                    if (current_AST_node.sub_nodes[1].token.type != Token.Type.LBRACKET)
                        to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                    else{
                        sb.add_instruction($"{stack_size} ; {Op_code.ALLOC_ARRAY}");
                        to_IR(current_AST_node.sub_nodes[1].sub_nodes[1], null, sb, ref let_decl_counter, true);
                    }
                    Token let_decl_tok = current_AST_node.sub_nodes[0].token;
                    if (!m_id_positions.TryAdd(let_decl_tok.id, m_id_positions.Count))
                        throw new Syntax_error_exception($"On line <{let_decl_tok.line_number}> identifier <{let_decl_tok.id}> is already in use");
                    ++let_decl_counter;
                    break;

                case Token.Type.BOOL:  sb.add_instruction($"{stack_size} ; {Op_code.TO_BOOL}");  break;
                case Token.Type.CHAR:  sb.add_instruction($"{stack_size} ; {Op_code.TO_CHAR}");  break;
                case Token.Type.INT:   sb.add_instruction($"{stack_size} ; {Op_code.TO_INT}");   break;
                case Token.Type.FLOAT: sb.add_instruction($"{stack_size} ; {Op_code.TO_FLOAT}"); break;
                case Token.Type.STR:   sb.add_instruction($"{stack_size} ; {Op_code.TO_STR}");   break;

                case Token.Type.PRINT:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{--stack_size} ; {Op_code.PRINT}");
                    break;
                case Token.Type.SCAN:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size} ; {Op_code.SCAN}");
                    break;

                case Token.Type.ARRAY_SIZE:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size} ; {Op_code.ARRAY_SIZE}");
                    break;
                case Token.Type.RAND:
                    sb.add_instruction($"{++stack_size} ; {Op_code.RAND}");
                    break;
                case Token.Type.POLL_CHAR:
                    sb.add_instruction($"{++stack_size} ; {Op_code.POLL_CHAR}");
                    break;

                case Token.Type.IF:
                    StringBuilder if_else_sb = new();
                    bool has_else_after = (next_AST_node is not null && next_AST_node.token.type == Token.Type.ELSE);

                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    --stack_size;

                    if (current_AST_node.sub_nodes.Length > 1){
                        int if_let_decl_counter = 0;
                        to_IR(current_AST_node.sub_nodes[1], null, if_else_sb, ref if_let_decl_counter, false);
                        while (if_let_decl_counter-- > 0){
                            if_else_sb.add_instruction($"{--stack_size} ; {Op_code.POP}");
                            m_id_positions.RemoveAt(m_id_positions.Count - 1);
                        }
                    }
                    sb.add_instruction($"{stack_size} ; {Op_code.JMPZ} {if_else_sb.count_instructions() + Convert.ToInt32(has_else_after)}");
                    sb.Append(if_else_sb);

                    if (has_else_after){
                        if_else_sb.Clear();
                        if (next_AST_node!.sub_nodes.Length > 0){
                            int else_let_decl_counter = 0;
                            to_IR(
                                next_AST_node.sub_nodes[0],
                                (next_AST_node!.sub_nodes.Length > 1) ? next_AST_node!.sub_nodes[1] : null,
                                if_else_sb,
                                ref else_let_decl_counter,
                                false
                            );
                            while (else_let_decl_counter-- > 0){
                                if_else_sb.add_instruction($"{--stack_size} ; {Op_code.POP}");
                                m_id_positions.RemoveAt(m_id_positions.Count - 1);
                            }
                        }
                        sb.add_instruction($"{stack_size} ; {Op_code.JMP} {if_else_sb.count_instructions()}");
                        sb.Append(if_else_sb);
                    }
                    break;
                case Token.Type.ELSE:
                    break;
                case Token.Type.WHILE:
                    StringBuilder while_condition_sb = new();
                    to_IR(current_AST_node.sub_nodes[0], null, while_condition_sb, ref let_decl_counter, true);
                    --stack_size;
                    
                    StringBuilder while_sb = new();

                    if (current_AST_node.sub_nodes.Length > 1){
                        int while_let_decl_counter = 0;
                        to_IR(current_AST_node.sub_nodes[1], null, while_sb, ref while_let_decl_counter, false);
                        while (while_let_decl_counter-- > 0){
                            while_sb.add_instruction($"{--stack_size} ; {Op_code.POP}");
                            m_id_positions.RemoveAt(m_id_positions.Count - 1);
                        }
                    }

                    while_condition_sb.add_instruction($"{stack_size} ; {Op_code.JMPZ} {while_sb.count_instructions() + 1}");
                    sb.Append(while_condition_sb);
                    sb.Append(while_sb);
                    sb.add_instruction($"{stack_size} ; {Op_code.JMP} -{while_condition_sb.count_instructions() + while_sb.count_instructions() - 2}");
                    break;

                case Token.Type.RETURN:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{--stack_size} ; {Op_code.RET}");
                    break;

                default:
                    throw new NotImplementedException($"Case for <{current_AST_node.token.type}> is not implemented");
            }
        }
    }

    public static string to_IR(ReadOnlySpan<Node> AST){
        State state = new();

        StringBuilder sb = new();
        int let_decl_counter = 0;

        // TODO: check discarded expressions (example: (a[0] + 1);)
        for (int i = 0; i < AST.Length - 1; ++i)
            if (AST[i].token.type != Token.Type.ELSE)
                state.to_IR(AST[i], AST[i + 1], sb, ref let_decl_counter, false);
        state.to_IR(AST[^1], null, sb, ref let_decl_counter, false);

        for (int i = state.stack_size; i-- > 0;)
            sb.add_instruction($"{i} ; {Op_code.POP}");

        return sb.ToString();
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
                case PUSH_STR:
                    if (rhs != ARGV_STR && rhs != FALSE_STR && rhs != TRUE_STR){
                        if (rhs.StartsWith('"'))
                            instruction_byte_count += (sizeof(int) + Encoding.UTF8.GetBytes(rhs.get_string_literal()).Length);
                        else{
                            instruction_byte_count += (
                                (rhs.StartsWith('\''))
                                    ? sizeof(char)
                                    : (rhs.StartsWith(SP_STR) || rhs.IndexOf('.') < 0)
                                        ? sizeof(int)
                                        : sizeof(float)
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
                case PUSH_STR:
                    if (rhs.StartsWith(SP_STR)){
                        as_bytes = BitConverter.GetBytes(int.Parse(rhs[3..^1]));
                        bytecode.Add((byte)Op_code.PUSH_FROM_SP);
                        bytecode.AddRange(as_bytes);
                    }
                    else if (rhs == ARGV_STR)
                        bytecode.Add((byte)Op_code.PUSH_ARGV);
                    else if (rhs == FALSE_STR)
                        bytecode.Add((byte)Op_code.PUSH_FALSE);
                    else if (rhs == TRUE_STR)
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
            }
        }

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bytecode);
    }
}
