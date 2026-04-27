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

    public enum Op_code : byte{
        PUSH_FROM_SP,
        PUSH_ARGC,
        PUSH_FALSE,
        PUSH_TRUE,
        PUSH_CHAR,
        PUSH_INT,
        PUSH_FLOAT,
        PUSH_STR,

        POP,

        RET,

        MOV,

        JMP,
        JMPZ,

        PRINT,
        SCAN,

        GET_ARGV,

        TO_BOOL,
        TO_CHAR,
        TO_INT,
        TO_FLOAT,
        TO_STR,

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
                    sb.add_instruction($"{stack_size} ; PUSH SP[-{stack_size - m_id_positions[current_AST_node.token.id] - 1}]");
                    break;

                case Token.Type.ARGC:
                    sb.add_instruction($"{++stack_size} ; PUSH ARGC");
                    break;

                case Token.Type.FALSE:
                case Token.Type.TRUE:
                    sb.add_instruction($"{++stack_size} ; PUSH {current_AST_node.token.id.ToUpper()}");
                    break;
                case Token.Type.CHAR_LIT:
                    sb.add_instruction($"{++stack_size} ; PUSH '{current_AST_node.token.id}'");
                    break;
                case Token.Type.INT_LIT:
                case Token.Type.FLOAT_LIT:
                    sb.add_instruction($"{++stack_size} ; PUSH {current_AST_node.token.id}");
                    break;
                case Token.Type.STR_LIT:
                    sb.add_instruction($"{++stack_size} ; PUSH \"{current_AST_node.token.id}\"");
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
                        block_sb.add_instruction($"{--stack_size} ; POP");
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
                // case Token.Type.AND:
                // case Token.Type.OR:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{--stack_size} ; {current_AST_node.token.type switch{
                        Token.Type.EQUALS          => "CMP_EQ",
                        Token.Type.NOT_EQUALS      => "CMP_NEQ",
                        Token.Type.LESS_THAN       => "CMP_LE",
                        Token.Type.LESS_THAN_EQ    => "CMP_LEQ",
                        Token.Type.GREATER_THAN    => "CMP_GE",
                        Token.Type.GREATER_THAN_EQ => "CMP_GEQ",
                        Token.Type.ASTERISK1       => "MUL",
                        Token.Type.ASTERISK2       => "POW",
                        Token.Type.SLASH           => "DIV",
                        Token.Type.PERCENT         => "MOD",
                        Token.Type.SHIFT_LEFT      => "SHL",
                        Token.Type.SHIFT_RIGHT     => "SHR",
                        Token.Type.AMPERSAND       => "BAND",
                        Token.Type.PIPE            => "BOR",
                        Token.Type.CARET           => "XOR",
                        // Token.Type.AND             => "AND",
                        // Token.Type.OR              => "OR",
                        
                        _ => throw new System.Diagnostics.UnreachableException(),
                    }}");
                    break;

                case Token.Type.TILDE:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size} ; BNEG");
                    break;

                case Token.Type.AND:
                case Token.Type.OR:
                    StringBuilder and_or_sb = new();
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    --stack_size;
                    to_IR(current_AST_node.sub_nodes[1], null, and_or_sb, ref let_decl_counter, true);
                    if (sb.ToString()[(sb.ToString().LastIndexOf(';') + 1)..].Trim() != "TO_BOOL")
                        sb.add_instruction($"{stack_size} ; TO_BOOL");
                    if (current_AST_node.token.type == Token.Type.AND){
                        sb.add_instruction($"{stack_size} ; NEG");
                        sb.add_instruction($"{stack_size - 1} ; JMPZ 3");
                        sb.add_instruction($"{stack_size} ; PUSH FALSE");
                    }
                    else{
                        sb.add_instruction($"{stack_size - 1} ; JMPZ 3");
                        sb.add_instruction($"{stack_size} ; PUSH TRUE");
                    }
                    sb.add_instruction($"{stack_size} ; JMP {and_or_sb.count_instructions() + 1}");
                    sb.Append(and_or_sb);
                    if (sb.ToString()[(sb.ToString().LastIndexOf(';') + 1)..].Trim() != "TO_BOOL")
                        sb.add_instruction($"{stack_size} ; TO_BOOL");
                    break;
                case Token.Type.NOT:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size} ; TO_BOOL");
                    sb.add_instruction($"{stack_size} ; NEG");
                    break;

                case Token.Type.PLUS:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    if (current_AST_node.sub_nodes.Length > 1){
                        to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                        sb.add_instruction($"{--stack_size} ; ADD");
                    }
                    break;
                case Token.Type.MINUS:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    if (current_AST_node.sub_nodes.Length > 1){
                        to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                        sb.add_instruction($"{--stack_size} ; SUB");
                    }
                    else
                        sb.add_instruction($"{stack_size} ; NEG");
                    break;

                case Token.Type.EQ:
                    Token eq_tok = current_AST_node.sub_nodes[0].token;
                    if (eq_tok.type != Token.Type.ID)
                        throw new Syntax_error_exception($"On line <{eq_tok.line_number}> trying to assign to rvalue");
                    if (!m_id_positions.ContainsKey(eq_tok.id))
                        throw new Syntax_error_exception($"On line <{eq_tok.line_number}> use of undeclared identifier <{eq_tok.id}>");
                    to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size - 1} ; MOV SP[-{stack_size - m_id_positions[eq_tok.id]}]");
                    --stack_size;
                    if (push_back_after_assignment){
                        sb.add_instruction($"{stack_size + 1} ; PUSH SP[-{stack_size - m_id_positions[eq_tok.id]}]");
                        ++stack_size;
                    }
                    break;

                case Token.Type.LET_DECL:
                    to_IR(current_AST_node.sub_nodes[1].sub_nodes[0], null, sb, ref let_decl_counter, true);
                    to_IR(current_AST_node.sub_nodes[1], null, sb, ref let_decl_counter, true);
                    Token let_decl_tok = current_AST_node.sub_nodes[0].token;
                    if (!m_id_positions.TryAdd(let_decl_tok.id, m_id_positions.Count))
                        throw new Syntax_error_exception($"On line <{let_decl_tok.line_number}> identifier <{let_decl_tok.id}> is already in use");
                    ++let_decl_counter;
                    break;

                case Token.Type.BOOL:  sb.add_instruction($"{stack_size} ; TO_BOOL");  break;
                case Token.Type.CHAR:  sb.add_instruction($"{stack_size} ; TO_CHAR");  break;
                case Token.Type.INT:   sb.add_instruction($"{stack_size} ; TO_INT");   break;
                case Token.Type.FLOAT: sb.add_instruction($"{stack_size} ; TO_FLOAT"); break;
                case Token.Type.STR:   sb.add_instruction($"{stack_size} ; TO_STR");   break;

                case Token.Type.PRINT:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{--stack_size} ; PRINT");
                    break;
                case Token.Type.SCAN:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size} ; SCAN");
                    break;

                case Token.Type.ARGV:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{stack_size} ; GET_ARGV");
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
                            if_else_sb.add_instruction($"{--stack_size} ; POP");
                            m_id_positions.RemoveAt(m_id_positions.Count - 1);
                        }
                    }
                    sb.add_instruction($"{stack_size} ; JMPZ {if_else_sb.count_instructions() + Convert.ToInt32(has_else_after)}");
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
                                if_else_sb.add_instruction($"{--stack_size} ; POP");
                                m_id_positions.RemoveAt(m_id_positions.Count - 1);
                            }
                        }
                        sb.add_instruction($"{stack_size} ; JMP {if_else_sb.count_instructions()}");
                        sb.Append(if_else_sb);
                    }
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
                            while_sb.add_instruction($"{--stack_size} ; POP");
                            m_id_positions.RemoveAt(m_id_positions.Count - 1);
                        }
                    }

                    while_condition_sb.add_instruction($"{stack_size} ; JMPZ {while_sb.count_instructions() + 1}");
                    sb.Append(while_condition_sb);
                    sb.Append(while_sb);
                    sb.add_instruction($"{stack_size} ; JMP -{while_condition_sb.count_instructions() + while_sb.count_instructions() - 2}");
                    break;

                case Token.Type.RETURN:
                    to_IR(current_AST_node.sub_nodes[0], null, sb, ref let_decl_counter, true);
                    sb.add_instruction($"{--stack_size} ; RET");
                    break;
            }
        }
    }

    public static string to_IR(ReadOnlySpan<Node> AST){
        State state = new();

        StringBuilder sb = new();
        int let_decl_counter = 0;

        for (int i = 0; i < AST.Length - 1; ++i)
            if (AST[i].token.type != Token.Type.ELSE)
                state.to_IR(AST[i], AST[i + 1], sb, ref let_decl_counter, false);
        state.to_IR(AST[^1], null, sb, ref let_decl_counter, false);

        for (int i = state.stack_size; i-- > 0;)
            sb.add_instruction($"{i} ; POP");

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
                case "PUSH":
                    if (rhs != "ARGC" && rhs != "FALSE" && rhs != "TRUE"){
                        if (rhs.StartsWith("\""))
                            instruction_byte_count += (sizeof(int) + Encoding.UTF8.GetBytes(rhs.get_string_literal()).Length);
                        else{
                            instruction_byte_count += (
                                (rhs.StartsWith('\''))
                                    ? sizeof(char)
                                    : (rhs.StartsWith("SP") || rhs.IndexOf('.') < 0)
                                        ? sizeof(int)
                                        : sizeof(float)
                            );
                        }
                    }
                    break;
                case "MOV":
                case "JMP":
                case "JMPZ":
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
                case "PUSH":
                    if (rhs.StartsWith("SP")){
                        as_bytes = BitConverter.GetBytes(int.Parse(rhs[3..(rhs.Length - 1)]));
                        bytecode.Add((byte)Op_code.PUSH_FROM_SP);
                        bytecode.AddRange(as_bytes);
                    }
                    else if (rhs == "ARGC")
                        bytecode.Add((byte)Op_code.PUSH_ARGC);
                    else if (rhs == "FALSE")
                        bytecode.Add((byte)Op_code.PUSH_FALSE);
                    else if (rhs == "TRUE")
                        bytecode.Add((byte)Op_code.PUSH_TRUE);
                    else if (rhs.StartsWith('\'')){
                        as_bytes = BitConverter.GetBytes(rhs.get_char_literal());
                        bytecode.Add((byte)Op_code.PUSH_CHAR);
                        bytecode.AddRange(as_bytes);
                    }
                    else if (rhs.StartsWith("\"")){
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

                case "POP": bytecode.Add((byte)Op_code.POP); break;
                case "RET": bytecode.Add((byte)Op_code.RET); break;

                case "MOV":
                    as_bytes = BitConverter.GetBytes(int.Parse(rhs[3..(rhs.Length - 1)]));
                    bytecode.Add((byte)Op_code.MOV);
                    bytecode.AddRange(as_bytes);
                    break;
                case "JMP":
                case "JMPZ":
                    int jmp_count = int.Parse(rhs);
                    int jmp_byte_count = (jmp_count >= 0)
                        ? count_bytes_in_instructions(instructions.AsSpan()[(i + 1)..][..(jmp_count - 1)]) - 1
                        : -(count_bytes_in_instructions(instructions.AsSpan()[(i + jmp_count)..][..(1 - jmp_count)]) + 1)
                    ;
                    as_bytes = BitConverter.GetBytes(jmp_byte_count);
                    bytecode.Add((lhs == "JMP") ? (byte)Op_code.JMP : (byte)Op_code.JMPZ);
                    bytecode.AddRange(as_bytes);
                    break;

                case "PRINT":    bytecode.Add((byte)Op_code.PRINT);    break;
                case "SCAN":     bytecode.Add((byte)Op_code.SCAN);     break;
                case "GET_ARGV": bytecode.Add((byte)Op_code.GET_ARGV); break;
                case "TO_BOOL":  bytecode.Add((byte)Op_code.TO_BOOL);  break;
                case "TO_CHAR":  bytecode.Add((byte)Op_code.TO_CHAR);  break;
                case "TO_INT":   bytecode.Add((byte)Op_code.TO_INT);   break;
                case "TO_FLOAT": bytecode.Add((byte)Op_code.TO_FLOAT); break;
                case "TO_STR":   bytecode.Add((byte)Op_code.TO_STR);   break;
                case "CMP_EQ":   bytecode.Add((byte)Op_code.CMP_EQ);   break;
                case "CMP_NEQ":  bytecode.Add((byte)Op_code.CMP_NEQ);  break;
                case "CMP_LE":   bytecode.Add((byte)Op_code.CMP_LE);   break;
                case "CMP_LEQ":  bytecode.Add((byte)Op_code.CMP_LEQ);  break;
                case "CMP_GE":   bytecode.Add((byte)Op_code.CMP_GE);   break;
                case "CMP_GEQ":  bytecode.Add((byte)Op_code.CMP_GEQ);  break;
                case "ADD":      bytecode.Add((byte)Op_code.ADD);      break;
                case "SUB":      bytecode.Add((byte)Op_code.SUB);      break;
                case "MUL":      bytecode.Add((byte)Op_code.MUL);      break;
                case "DIV":      bytecode.Add((byte)Op_code.DIV);      break;
                case "MOD":      bytecode.Add((byte)Op_code.MOD);      break;
                case "POW":      bytecode.Add((byte)Op_code.POW);      break;
                case "SHL":      bytecode.Add((byte)Op_code.SHL);      break;
                case "SHR":      bytecode.Add((byte)Op_code.SHR);      break;
                case "BAND":     bytecode.Add((byte)Op_code.BAND);     break;
                case "BOR":      bytecode.Add((byte)Op_code.BOR);      break;
                case "XOR":      bytecode.Add((byte)Op_code.XOR);      break;
                case "BNEG":     bytecode.Add((byte)Op_code.BNEG);     break;
                // case "AND":      bytecode.Add((byte)Op_code.AND);      break;
                // case "OR":       bytecode.Add((byte)Op_code.OR);       break;
                case "NEG":      bytecode.Add((byte)Op_code.NEG);      break;
            }
        }

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bytecode);
    }
}
