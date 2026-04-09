namespace Compile;

using Lex;
using Parse;
using System.Text;

static class Compiler_extensions{
    extension(StringBuilder self){
        public void add_instruction(string str) => self.AppendLine(Compiler.M_INDENT + str);
        public int count_instructions() => self.ToString().Split('\n').Length;
    }
}

public static class Compiler{
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
                    sb.add_instruction($"{stack_size} ; PRINT {print_sub_node.token.id}");
                else{
                    to_IR(print_sub_node, null, stack_info, sb, ref stack_size, ref let_decl_counter);
                    sb.add_instruction($"{stack_size - 1} ; PRINT");
                    --stack_size;
                }
                break;
            case Token.Type.SCAN:
                sb.add_instruction($"{stack_size + 1} ; SCAN {current_AST_node.token.id}");
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
                sb.add_instruction($"{stack_size} : JMP -{while_condition_sb.count_instructions() + while_sb.count_instructions() - 2}");
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
            was_if = (!was_if) ? to_IR(current_AST_node, next_AST_node, stack_info, sb, ref stack_size, ref let_decl_counter) : false;
            sb.AppendLine();
        }
        to_IR(AST.Last(), null, stack_info, sb, ref stack_size, ref let_decl_counter);

        while (stack_size > 0)
            sb.add_instruction($"{--stack_size} ; POP");

        return sb.ToString();
    }
    public static string to_IR(List<Token> tokens) => to_IR(Parser.build_AST(tokens));
    public static string to_IR(string path) => to_IR(Lexer.tokenize(path));
}
