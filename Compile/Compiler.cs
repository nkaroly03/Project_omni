namespace Compile;

using Lex;
using Parse;
using System.Text;

static class Compiler_extensions{
    extension(StringBuilder self){
        public void add_instruction(string str) => self.AppendLine(Compiler.M_INDENT + str);
        public int count_instructions() => self.ToString().Split(Environment.NewLine).Length;
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

        ADD,
        SUB,
        MUL,
        DIV,
        MOD,

        NEG,
        AND,
        OR,

        CMP_LE,
        CMP_LEQ,
        CMP_GE,
        CMP_GEQ,
        CMP_EQ,
        CMP_NEQ,
    }

    internal const string M_INDENT = "    ";

    static bool to_IR(Node current_AST_node, Node? next_AST_node, OrderedDictionary<string, int> stack_info, StringBuilder sb, ref int stack_size, ref int let_decl_counter){
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

            case Token.Type.LESS_THAN:
            case Token.Type.LESS_THAN_EQ:
            case Token.Type.GREATER_THAN:
            case Token.Type.GREATER_THAN_EQ:
            case Token.Type.NOT_EQUALS:
            case Token.Type.EQUALS:
            case Token.Type.ASTERISK:
            case Token.Type.SLASH:
            case Token.Type.PERCENT:
            case Token.Type.AND:
            case Token.Type.OR:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                switch (current_AST_node.token.type){
                    case Token.Type.LESS_THAN:
                        sb.add_instruction($"{stack_size - 1} ; CMP_LE");
                        break;
                    case Token.Type.LESS_THAN_EQ:
                        sb.add_instruction($"{stack_size - 1} ; CMP_LEQ");
                        break;
                    case Token.Type.GREATER_THAN:
                        sb.add_instruction($"{stack_size - 1} ; CMP_GE");
                        break;
                    case Token.Type.GREATER_THAN_EQ:
                        sb.add_instruction($"{stack_size - 1} ; CMP_GEQ");
                        break;
                    case Token.Type.NOT_EQUALS:
                        sb.add_instruction($"{stack_size - 1} ; CMP_NEQ");
                        break;
                    case Token.Type.EQUALS:
                        sb.add_instruction($"{stack_size - 1} ; CMP_EQ");
                        break;
                    case Token.Type.ASTERISK:
                        sb.add_instruction($"{stack_size - 1} ; MUL");
                        break;
                    case Token.Type.SLASH:
                        sb.add_instruction($"{stack_size - 1} ; DIV");
                        break;
                    case Token.Type.PERCENT:
                        sb.add_instruction($"{stack_size - 1} ; MOD");
                        break;
                    case Token.Type.AND:
                        sb.add_instruction($"{stack_size - 1} ; AND");
                        break;
                    case Token.Type.OR:
                        sb.add_instruction($"{stack_size - 1} ; OR");
                        break;
                }
                --stack_size;
                break;

            case Token.Type.EQ:
                to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.add_instruction($"{stack_size - 1} ; MOV SP[-{stack_size - stack_info[current_AST_node.sub_nodes[0].token.id]}]");
                --stack_size;
                break;

            case Token.Type.NOT:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.add_instruction($"{stack_size} ; NEG");
                break;

            case Token.Type.PLUS:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                if (current_AST_node.sub_nodes.Count > 1){
                    to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                    sb.add_instruction($"{stack_size - 1} ; ADD");
                    --stack_size;
                }
                break;
            case Token.Type.MINUS:
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                if (current_AST_node.sub_nodes.Count > 1){
                    to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                    sb.add_instruction($"{stack_size - 1} ; SUB");
                    --stack_size;
                }
                else
                    sb.add_instruction($"{stack_size} ; NEG");
                break;


            case Token.Type.LET_DECL:
                to_IR(current_AST_node.sub_nodes[1].sub_nodes[0], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                to_IR(current_AST_node.sub_nodes[1], null, stack_info, sb, ref stack_size, ref let_decl_counter);
                stack_info.Add(current_AST_node.sub_nodes[0].token.id, stack_info.Count);
                ++let_decl_counter;
                break;

            case Token.Type.BOOL:
                sb.add_instruction($"{stack_size} ; TO_BOOL");
                break;
            case Token.Type.INT:
                sb.add_instruction($"{stack_size} ; TO_INT");
                break;
            case Token.Type.FLOAT:
                sb.add_instruction($"{stack_size} ; TO_FLOAT");
                break;

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
                List<Node> if_sub_nodes = current_AST_node.sub_nodes[1..];
                if (if_sub_nodes.Count > 0){
                    bool was_if = false;
                    foreach ((Node if_sub_node_current, Node if_sub_node_next) in if_sub_nodes[..(if_sub_nodes.Count - 1)].Zip(if_sub_nodes[1..]))
                        was_if = (!was_if) ? to_IR(if_sub_node_current, if_sub_node_next, stack_info, if_else_sb, ref stack_size, ref if_let_decl_counter) : false;
                    if (!was_if)
                        to_IR(if_sub_nodes.Last(), null, stack_info, if_else_sb, ref stack_size, ref if_let_decl_counter);
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

                    List<Node> else_sub_nodes = next_AST_node!.sub_nodes;
                    if (else_sub_nodes.Count > 0){
                        bool was_if = false;
                        foreach ((Node else_sub_node_current, Node else_sub_node_next) in else_sub_nodes[..(else_sub_nodes.Count - 1)].Zip(else_sub_nodes[1..]))
                            was_if = (!was_if) ? to_IR(else_sub_node_current, else_sub_node_next, stack_info, if_else_sb, ref stack_size, ref else_let_decl_counter) : false;
                        if (!was_if)
                            to_IR(else_sub_nodes.Last(), null, stack_info, if_else_sb, ref stack_size, ref else_let_decl_counter);
                    }
                    while (else_let_decl_counter-- > 0){
                        if_else_sb.add_instruction($"{--stack_size} ; POP");
                        stack_info.RemoveAt(stack_info.Count - 1);
                    }
                    sb.add_instruction($"{stack_size} ; JMP {if_else_sb.count_instructions()}");
                    sb.Append(if_else_sb);
                }
                return true;
            case Token.Type.WHILE:
                StringBuilder while_condition_sb = new();
                to_IR(current_AST_node.sub_nodes[0], null, stack_info, while_condition_sb, ref stack_size, ref let_decl_counter);
                --stack_size;
                
                StringBuilder while_sb = new();

                List<Node> while_sub_nodes = current_AST_node.sub_nodes[1..];
                if (while_sub_nodes.Count > 0){
                    int while_let_decl_counter = 0;

                    foreach ((Node while_sub_node_current, Node while_sub_node_next) in while_sub_nodes[..(while_sub_nodes.Count - 1)].Zip(while_sub_nodes[1..]))
                        to_IR(while_sub_node_current, while_sub_node_next, stack_info, while_sb, ref stack_size, ref while_let_decl_counter);
                    to_IR(while_sub_nodes.Last(), null, stack_info, while_sb, ref stack_size, ref while_let_decl_counter);

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
                sb.add_instruction($"{stack_size - 1} ; RET");
                --stack_size;
                break;
        }

        return false;
    }

    public static string to_IR(List<Node> AST){
        OrderedDictionary<string, int> stack_info = new();

        StringBuilder sb = new();

        int stack_size = 0;
        int let_decl_counter = 0;

        bool was_if = false;
        foreach ((Node current_AST_node, Node next_AST_node) in AST[..(AST.Count - 1)].Zip(AST[1..])){
            if (!was_if){
                was_if = to_IR(current_AST_node, next_AST_node, stack_info, sb, ref stack_size, ref let_decl_counter);
                sb.AppendLine();
            }
            else
                was_if = false;
        }
        to_IR(AST.Last(), null, stack_info, sb, ref stack_size, ref let_decl_counter);

        while (stack_size > 0)
            sb.add_instruction($"{--stack_size} ; POP");

        return sb.ToString();
    }
    public static string to_IR(List<Token> tokens) => to_IR(Parser.build_AST(tokens));
    public static string to_IR(string path) => to_IR(Lexer.tokenize(path));

    static int count_bytes_in_instructions(string[] instructions){
        int instruction_byte_count = 0;

        foreach (string instruction in instructions){
            instruction_byte_count += sizeof(Op_code);
            string[] split_instruction = [
                instruction.Split(' ', StringSplitOptions.TrimEntries)[0],
                (instruction.IndexOf(' ') >= 0) ? instruction[(instruction.IndexOf(' ') + 1)..].Trim() : ""
            ];
            (string lhs, string rhs) = (split_instruction[0], split_instruction[1]);
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
                    instruction_byte_count += (Convert.ToInt32(rhs.Length > 0) * (sizeof(int) + Encoding.UTF8.GetBytes(rhs.Trim('"')).Length));
                    break;
                case "SCAN":
                    instruction_byte_count += (sizeof(int) + Encoding.UTF8.GetBytes(rhs).Length);
                    break;
            }
        }

        return instruction_byte_count;
    }

    public static byte[] to_bytecode(string IR){
        List<byte> bytecode = new();
        string[] instructions = IR.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < instructions.Length; ++i)
            instructions[i] = instructions[i].Split(';')[1].Trim();
        foreach ((int i, string instruction) in instructions.Index()){
            string[] split_instruction = [
                instruction.Split(' ', StringSplitOptions.TrimEntries)[0],
                (instruction.IndexOf(' ') >= 0) ? instruction[(instruction.IndexOf(' ') + 1)..].Trim() : ""
            ];
            (string lhs, string rhs) = (split_instruction[0], split_instruction[1]);
            byte[] as_bytes;
            switch (lhs){
                case "PUSH":
                    if (rhs.StartsWith("SP")){
                        as_bytes = BitConverter.GetBytes(Convert.ToInt32(rhs[3..(rhs.Length - 1)]));
                        bytecode.Add((byte)Op_code.PUSH_FROM_SP);
                        bytecode.AddRange(as_bytes);
                    }
                    else if (rhs == "FALSE")
                        bytecode.Add((byte)Op_code.PUSH_FALSE);
                    else if (rhs == "TRUE")
                        bytecode.Add((byte)Op_code.PUSH_TRUE);
                    else if (rhs.IndexOf('.') < 0){
                        as_bytes = BitConverter.GetBytes(Convert.ToInt32(rhs));
                        bytecode.Add((byte)Op_code.PUSH_INT);
                        bytecode.AddRange(as_bytes);
                    }
                    else{
                        as_bytes = BitConverter.GetBytes(Convert.ToSingle(rhs, System.Globalization.CultureInfo.InvariantCulture));
                        bytecode.Add((byte)Op_code.PUSH_FLOAT);
                        bytecode.AddRange(as_bytes);
                    }
                    break;
                case "POP":
                    bytecode.Add((byte)Op_code.POP);
                    break;
                case "RET":
                    bytecode.Add((byte)Op_code.RET);
                    break;
                case "MOV":
                    as_bytes = BitConverter.GetBytes(Convert.ToInt32(rhs[3..(rhs.Length - 1)]));
                    bytecode.Add((byte)Op_code.MOV);
                    bytecode.AddRange(as_bytes);
                    break;
                case "JMP":
                case "JMPZ":
                    int jmp_count = Convert.ToInt32(rhs);
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
                        as_bytes = Encoding.UTF8.GetBytes(rhs.Trim('"'));
                        bytecode.Add((byte)Op_code.PRINT_STR);
                        bytecode.AddRange(BitConverter.GetBytes(as_bytes.Length));
                        bytecode.AddRange(as_bytes);
                    }
                    else
                        bytecode.Add((byte)Op_code.PRINT);
                    break;
                case "SCAN":
                    as_bytes = Encoding.UTF8.GetBytes(rhs.Trim('"'));
                    bytecode.Add((byte)Op_code.SCAN);
                    bytecode.AddRange(BitConverter.GetBytes(as_bytes.Length));
                    bytecode.AddRange(as_bytes);
                    break;
                case "TO_BOOL":
                    bytecode.Add((byte)Op_code.TO_BOOL);
                    break;
                case "TO_INT":
                    bytecode.Add((byte)Op_code.TO_INT);
                    break;
                case "TO_FLOAT":
                    bytecode.Add((byte)Op_code.TO_FLOAT);
                    break;
                case "ADD":
                    bytecode.Add((byte)Op_code.ADD);
                    break;
                case "SUB":
                    bytecode.Add((byte)Op_code.SUB);
                    break;
                case "MUL":
                    bytecode.Add((byte)Op_code.MUL);
                    break;
                case "DIV":
                    bytecode.Add((byte)Op_code.DIV);
                    break;
                case "MOD":
                    bytecode.Add((byte)Op_code.MOD);
                    break;
                case "NEG":
                    bytecode.Add((byte)Op_code.NEG);
                    break;
                case "AND":
                    bytecode.Add((byte)Op_code.AND);
                    break;
                case "OR":
                    bytecode.Add((byte)Op_code.OR);
                    break;
                case "CMP_LE":
                    bytecode.Add((byte)Op_code.CMP_LE);
                    break;
                case "CMP_LEQ":
                    bytecode.Add((byte)Op_code.CMP_LEQ);
                    break;
                case "CMP_GE":
                    bytecode.Add((byte)Op_code.CMP_GE);
                    break;
                case "CMP_GEQ":
                    bytecode.Add((byte)Op_code.CMP_GEQ);
                    break;
                case "CMP_EQ":
                    bytecode.Add((byte)Op_code.CMP_EQ);
                    break;
                case "CMP_NEQ":
                    bytecode.Add((byte)Op_code.CMP_NEQ);
                    break;
            }
        }

        return bytecode.ToArray();
    }
}
