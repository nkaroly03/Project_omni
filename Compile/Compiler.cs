namespace Compile;

using Lex;
using Parse;
using System.Text;

static class Compiler_extensions{
    extension(StringBuilder self){
        public void add_instruction(string str) => self.AppendLine($"    {str}");
        public int count_instructions() => self.ToString().Split(Environment.NewLine).Length;
    }
    extension(string self){
        public string get_string_literal() => System.Text.RegularExpressions.Regex.Unescape(self[1..(self.Length - 1)]);
    }
}

public static class Compiler{
    public enum Op_code : byte{
        PUSH_FROM_SP,
        PUSH_FALSE,
        PUSH_TRUE,
        PUSH_INT,
        PUSH_FLOAT,

        POP,

        RET,

        MOV,

        JMP,
        JMPZ,

        PRINT_STR,
        PRINT,

        SCAN,

        TO_BOOL,
        TO_INT,
        TO_FLOAT,

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

        SHL,
        SHR,
        BAND,
        BOR,
        XOR,
        BNEG,

        AND,
        OR,
        NEG,
    }

    static void to_IR(Node current_AST_node, Node? next_AST_node, OrderedDictionary<string, int> stack_info, StringBuilder sb, ref int stack_size, ref int let_decl_counter){
        switch (current_AST_node.token.type){
            case Token.Type.ID:
                ++stack_size;
                sb.add_instruction($"{stack_size} ; PUSH SP[-{stack_size - stack_info[current_AST_node.token.id] - 1}]");
                break;

            case Token.Type.FALSE:
                ++stack_size;
                sb.add_instruction($"{stack_size} ; PUSH FALSE");
                break;
            case Token.Type.TRUE:
                ++stack_size;
                sb.add_instruction($"{stack_size} ; PUSH TRUE");
                break;
            case Token.Type.INT_LIT:
            case Token.Type.FLOAT_LIT:
                ++stack_size;
                sb.add_instruction($"{stack_size} ; PUSH {current_AST_node.token.id}");
                break;

            case Token.Type.EQUALS:
            case Token.Type.NOT_EQUALS:
            case Token.Type.LESS_THAN:
            case Token.Type.LESS_THAN_EQ:
            case Token.Type.GREATER_THAN:
            case Token.Type.GREATER_THAN_EQ:
            case Token.Type.ASTERISK:
            case Token.Type.SLASH:
            case Token.Type.PERCENT:
            case Token.Type.SHIFT_LEFT:
            case Token.Type.SHIFT_RIGHT:
            case Token.Type.BITWISE_AND:
            case Token.Type.BITWISE_OR:
            case Token.Type.XOR:
            case Token.Type.AND:
            case Token.Type.OR:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.add_instruction($"{stack_size - 1} ; {current_AST_node.token.type switch{
                    Token.Type.EQUALS          => "CMP_EQ",
                    Token.Type.NOT_EQUALS      => "CMP_NEQ",
                    Token.Type.LESS_THAN       => "CMP_LE",
                    Token.Type.LESS_THAN_EQ    => "CMP_LEQ",
                    Token.Type.GREATER_THAN    => "CMP_GE",
                    Token.Type.GREATER_THAN_EQ => "CMP_GEQ",
                    Token.Type.ASTERISK        => "MUL",
                    Token.Type.SLASH           => "DIV",
                    Token.Type.PERCENT         => "MOD",
                    Token.Type.SHIFT_LEFT      => "SHL",
                    Token.Type.SHIFT_RIGHT     => "SHR",
                    Token.Type.BITWISE_AND     => "BAND",
                    Token.Type.BITWISE_OR      => "BOR",
                    Token.Type.XOR             => "XOR",
                    Token.Type.AND             => "AND",
                    Token.Type.OR              => "OR",
                    
                    _ => throw new System.Diagnostics.UnreachableException(),
                }}");
                --stack_size;
                break;

            case Token.Type.BITWISE_NEG:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.add_instruction($"{stack_size} ; BNEG");
                break;
            case Token.Type.NOT:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.add_instruction($"{stack_size} ; NEG");
                break;

            case Token.Type.PLUS:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                if (current_AST_node.sub_nodes.Length > 1){
                    to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                    sb.add_instruction($"{stack_size - 1} ; ADD");
                    --stack_size;
                }
                break;
            case Token.Type.MINUS:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                if (current_AST_node.sub_nodes.Length > 1){
                    to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                    sb.add_instruction($"{stack_size - 1} ; SUB");
                    --stack_size;
                }
                else
                    sb.add_instruction($"{stack_size} ; NEG");
                break;

            case Token.Type.EQ:
                to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.add_instruction($"{stack_size - 1} ; MOV SP[-{stack_size - stack_info[current_AST_node.sub_nodes[0].token.id]}]");
                --stack_size;
                break;

            case Token.Type.LET_DECL:
                to_IR(current_AST_node.sub_nodes[1].sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                stack_info.Add(current_AST_node.sub_nodes[0].token.id, stack_info.Count);
                ++let_decl_counter;
                break;

            case Token.Type.BOOL:  sb.add_instruction($"{stack_size} ; TO_BOOL");  break;
            case Token.Type.INT:   sb.add_instruction($"{stack_size} ; TO_INT");   break;
            case Token.Type.FLOAT: sb.add_instruction($"{stack_size} ; TO_FLOAT"); break;

            case Token.Type.PRINT:
                Node print_sub_node = current_AST_node.sub_nodes[0];
                if (print_sub_node.token.type == Token.Type.STR_LIT)
                    sb.add_instruction($"{stack_size} ; PRINT \"{print_sub_node.token.id}\"");
                else{
                    to_IR(print_sub_node, null, stack_info, sb, ref stack_size, ref let_decl_counter);
                    sb.add_instruction($"{stack_size - 1} ; PRINT");
                    --stack_size;
                }
                break;
            case Token.Type.SCAN:
                sb.add_instruction($"{stack_size + 1} ; SCAN \"{current_AST_node.sub_nodes[0].token.id}\"");
                ++stack_size;
                break;

            case Token.Type.IF:
                StringBuilder if_else_sb = new();
                bool has_else_after = (next_AST_node is not null && next_AST_node.token.type == Token.Type.ELSE);

                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                --stack_size;

                int if_let_decl_counter = 0;
                ReadOnlySpan<Node> if_sub_nodes = current_AST_node.sub_nodes[1..];
                if (if_sub_nodes.Length > 0){
                    for (int i = 0; i < if_sub_nodes.Length - 1; ++i)
                        if (if_sub_nodes[i].token.type != Token.Type.ELSE)
                            to_IR(if_sub_nodes[i], if_sub_nodes[i + 1], stack_info, if_else_sb, ref stack_size, ref if_let_decl_counter);
                    to_IR(if_sub_nodes[^1], null, stack_info, if_else_sb, ref stack_size, ref if_let_decl_counter);
                }
                while (if_let_decl_counter-- > 0){
                    if_else_sb.add_instruction($"{--stack_size} ; POP");
                    stack_info.RemoveAt(stack_info.Count - 1);
                }
                sb.add_instruction($"{stack_size} ; JMPZ {if_else_sb.count_instructions() + Convert.ToInt32(has_else_after)}");
                sb.Append(if_else_sb);

                if (has_else_after){
                    int else_let_decl_counter = 0;

                    if_else_sb.Clear();

                    ReadOnlySpan<Node> else_sub_nodes = next_AST_node!.sub_nodes;
                    if (else_sub_nodes.Length > 0){
                        for (int i = 0; i < else_sub_nodes.Length - 1; ++i)
                            if (else_sub_nodes[i].token.type != Token.Type.ELSE)
                                to_IR(else_sub_nodes[i], else_sub_nodes[i + 1], stack_info, if_else_sb, ref stack_size, ref else_let_decl_counter);
                        to_IR(else_sub_nodes[^1], null, stack_info, if_else_sb, ref stack_size, ref else_let_decl_counter);
                    }
                    while (else_let_decl_counter-- > 0){
                        if_else_sb.add_instruction($"{--stack_size} ; POP");
                        stack_info.RemoveAt(stack_info.Count - 1);
                    }
                    sb.add_instruction($"{stack_size} ; JMP {if_else_sb.count_instructions()}");
                    sb.Append(if_else_sb);
                }
                break;
            case Token.Type.WHILE:
                StringBuilder while_condition_sb = new();
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, while_condition_sb, ref stack_size, ref let_decl_counter);
                --stack_size;
                
                StringBuilder while_sb = new();

                ReadOnlySpan<Node> while_sub_nodes = current_AST_node.sub_nodes[1..];
                if (while_sub_nodes.Length > 0){
                    int while_let_decl_counter = 0;

                    for (int i = 0; i < while_sub_nodes.Length - 1; ++i)
                        to_IR(while_sub_nodes[i], while_sub_nodes[i + 1], stack_info, while_sb, ref stack_size, ref while_let_decl_counter);
                    to_IR(while_sub_nodes[^1], null, stack_info, while_sb, ref stack_size, ref while_let_decl_counter);

                    while (while_let_decl_counter-- > 0){
                        while_sb.add_instruction($"{--stack_size} ; POP");
                        stack_info.RemoveAt(stack_info.Count - 1);
                    }
                }

                while_condition_sb.add_instruction($"{stack_size} ; JMPZ {while_sb.count_instructions() + 1}");
                sb.Append(while_condition_sb);
                sb.Append(while_sb);
                sb.add_instruction($"{stack_size} ; JMP -{while_condition_sb.count_instructions() + while_sb.count_instructions() - 2}");
                break;

            case Token.Type.RETURN:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.add_instruction($"{stack_size - 1} ; RET");
                --stack_size;
                break;
        }
    }

    public static string to_IR(ReadOnlySpan<Node> AST){
        OrderedDictionary<string, int> stack_info = new();

        StringBuilder sb = new();

        int stack_size = 0;
        int let_decl_counter = 0;

        for (int i = 0; i < AST.Length - 1; ++i){
            if (AST[i].token.type != Token.Type.ELSE){
                to_IR(AST[i], AST[i + 1], stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.AppendLine();
            }
        }
        to_IR(AST[^1], null, stack_info, sb, ref stack_size, ref let_decl_counter);

        while (stack_size > 0)
            sb.add_instruction($"{--stack_size} ; POP");

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
                    if (rhs != "FALSE" && rhs != "TRUE")
                        instruction_byte_count += ((rhs.StartsWith("SP") || rhs.IndexOf('.') < 0) ? sizeof(int) : sizeof(float));
                    break;
                case "MOV":
                case "JMP":
                case "JMPZ":
                    instruction_byte_count += sizeof(int);
                    break;
                case "PRINT":
                    if (rhs.Length > 0)
                        instruction_byte_count += (sizeof(int) + Encoding.UTF8.GetBytes(rhs.get_string_literal()).Length);
                    break;
                case "SCAN":
                    instruction_byte_count += (sizeof(int) + Encoding.UTF8.GetBytes(rhs.get_string_literal()).Length);
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
                    else if (rhs == "FALSE")
                        bytecode.Add((byte)Op_code.PUSH_FALSE);
                    else if (rhs == "TRUE")
                        bytecode.Add((byte)Op_code.PUSH_TRUE);
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
                        ? count_bytes_in_instructions(instructions[(i + 1)..][..(jmp_count - 1)]) - 1
                        : -(count_bytes_in_instructions(instructions[(i + jmp_count)..][..(1 - jmp_count)]) + 1)
                    ;
                    as_bytes = BitConverter.GetBytes(jmp_byte_count);
                    bytecode.Add((lhs == "JMP") ? (byte)Op_code.JMP : (byte)Op_code.JMPZ);
                    bytecode.AddRange(as_bytes);
                    break;
                case "PRINT":
                    if (rhs.Length > 0){
                        as_bytes = Encoding.UTF8.GetBytes(rhs.get_string_literal());
                        bytecode.Add((byte)Op_code.PRINT_STR);
                        bytecode.AddRange(BitConverter.GetBytes(as_bytes.Length));
                        bytecode.AddRange(as_bytes);
                    }
                    else
                        bytecode.Add((byte)Op_code.PRINT);
                    break;
                case "SCAN":
                    as_bytes = Encoding.UTF8.GetBytes(rhs.get_string_literal());
                    bytecode.Add((byte)Op_code.SCAN);
                    bytecode.AddRange(BitConverter.GetBytes(as_bytes.Length));
                    bytecode.AddRange(as_bytes);
                    break;

                case "TO_BOOL":  bytecode.Add((byte)Op_code.TO_BOOL);  break;
                case "TO_INT":   bytecode.Add((byte)Op_code.TO_INT);   break;
                case "TO_FLOAT": bytecode.Add((byte)Op_code.TO_FLOAT); break;
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
                case "SHL":      bytecode.Add((byte)Op_code.SHL);      break;
                case "SHR":      bytecode.Add((byte)Op_code.SHR);      break;
                case "BAND":     bytecode.Add((byte)Op_code.BAND);     break;
                case "BOR":      bytecode.Add((byte)Op_code.BOR);      break;
                case "XOR":      bytecode.Add((byte)Op_code.XOR);      break;
                case "BNEG":     bytecode.Add((byte)Op_code.BNEG);     break;
                case "AND":      bytecode.Add((byte)Op_code.AND);      break;
                case "OR":       bytecode.Add((byte)Op_code.OR);       break;
                case "NEG":      bytecode.Add((byte)Op_code.NEG);      break;
            }
        }

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bytecode);
    }
}
