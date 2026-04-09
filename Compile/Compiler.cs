namespace Compile;

using Lex;
using Parse;
using System.Text;

static class Compiler_extensions{
    extension(StringBuilder self){
        public void add_instruction(string str) => self.AppendLine(Compiler.M_INDENT + str);
    }
}

public static class Compiler{
    internal const string M_INDENT = "    ";

    struct Id{
        public required int stack_position, nest_count;
    }

    static void to_IR(Node AST_node, Dictionary<string, Id> id_infos, StringBuilder sb, ref int temp_push_count, ref int instruction_counter){
        StringBuilder sb_new = new();

        switch (AST_node.token.type){
            case Token.Type.ID:
                sb.add_instruction($"PUSH SP[{id_infos[AST_node.token.id].stack_position - id_infos.Count - temp_push_count}]");
                ++temp_push_count;
                ++instruction_counter;
                break;

            case Token.Type.FALSE:
                sb.add_instruction("PUSH FALSE");
                ++temp_push_count;
                ++instruction_counter;
                break;
            case Token.Type.TRUE:
                sb.add_instruction("PUSH TRUE");
                ++temp_push_count;
                ++instruction_counter;
                break;
            case Token.Type.INT_LIT:
            case Token.Type.FLOAT_LIT:
                sb.add_instruction($"PUSH {AST_node.token.id}");
                ++temp_push_count;
                ++instruction_counter;
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
                to_IR(AST_node.sub_nodes[0], id_infos, sb, ref temp_push_count, ref instruction_counter);
                to_IR(AST_node.sub_nodes[1], id_infos, sb, ref temp_push_count, ref instruction_counter);
                switch (AST_node.token.type){
                    case Token.Type.LESS_THAN:
                        sb.add_instruction("CMP_LE");
                        break;
                    case Token.Type.LESS_THAN_EQ:
                        sb.add_instruction("CMP_LEQ");
                        break;
                    case Token.Type.GREATER_THAN:
                        sb.add_instruction("CMP_GE");
                        break;
                    case Token.Type.GREATER_THAN_EQ:
                        sb.add_instruction("CMP_GEQ");
                        break;
                    case Token.Type.NOT_EQUALS:
                        sb.add_instruction("CMP_EQ");
                        break;
                    case Token.Type.EQUALS:
                        sb.add_instruction("CMP_NEQ");
                        break;
                    case Token.Type.ASTERISK:
                        sb.add_instruction("MUL");
                        break;
                    case Token.Type.SLASH:
                        sb.add_instruction("DIV");
                        break;
                    case Token.Type.PERCENT:
                        sb.add_instruction("MOD");
                        break;
                }
                --temp_push_count;
                ++instruction_counter;
                break;

            case Token.Type.EQ:
                to_IR(AST_node.sub_nodes[1], id_infos, sb, ref temp_push_count, ref instruction_counter);
                sb.add_instruction($"MOV SP[{id_infos[AST_node.sub_nodes[0].token.id].stack_position - id_infos.Count - temp_push_count}]");
                --temp_push_count;
                ++instruction_counter;
                break;

            case Token.Type.PLUS:
                to_IR(AST_node.sub_nodes[0], id_infos, sb, ref temp_push_count, ref instruction_counter);
                if (AST_node.sub_nodes.Count > 1){
                    to_IR(AST_node.sub_nodes[1], id_infos, sb, ref temp_push_count, ref instruction_counter);
                    sb.add_instruction("ADD");
                    --temp_push_count;
                    ++instruction_counter;
                }
                break;
            case Token.Type.MINUS:
                to_IR(AST_node.sub_nodes[0], id_infos, sb, ref temp_push_count, ref instruction_counter);
                if (AST_node.sub_nodes.Count > 1){
                    to_IR(AST_node.sub_nodes[1], id_infos, sb, ref temp_push_count, ref instruction_counter);
                    sb.add_instruction("SUB");
                    --temp_push_count;
                }
                else
                    sb.add_instruction("NEG");
                ++instruction_counter;
                break;


            case Token.Type.LET_DECL:
                to_IR(AST_node.sub_nodes[1].sub_nodes[0], id_infos, sb, ref temp_push_count, ref instruction_counter);
                to_IR(AST_node.sub_nodes[1], id_infos, sb, ref temp_push_count, ref instruction_counter);
                id_infos.Add(AST_node.sub_nodes[0].token.id, new(){stack_position = id_infos.Count, nest_count = 1});
                --temp_push_count;
                ++instruction_counter;
                break;

            case Token.Type.BOOL:
                sb.add_instruction("TO_BOOL");
                ++instruction_counter;
                break;
            case Token.Type.INT:
                sb.add_instruction("TO_INT");
                ++instruction_counter;
                break;
            case Token.Type.FLOAT:
                sb.add_instruction("TO_FLOAT");
                ++instruction_counter;
                break;

            case Token.Type.PRINT:
                Node print_sub_node = AST_node.sub_nodes[0];
                if (print_sub_node.token.type == Token.Type.STR_LIT)
                    sb.add_instruction($"PRINT {print_sub_node.token.id}");
                else{
                    to_IR(print_sub_node, id_infos, sb, ref temp_push_count, ref instruction_counter);
                    sb.add_instruction("PRINT");
                    --temp_push_count;
                }
                ++instruction_counter;
                break;
            case Token.Type.SCAN:
                sb.add_instruction($"SCAN {AST_node.token.id}");
                ++temp_push_count;
                ++instruction_counter;
                break;

            case Token.Type.IF:
                foreach (string key in id_infos.Keys.ToList()){
                    Id id = id_infos[key];
                    ++id.nest_count;
                    id_infos[key] = id;
                }
                to_IR(AST_node.sub_nodes[0], id_infos, sb, ref temp_push_count, ref instruction_counter);
                --temp_push_count;
                List<Node> if_sub_nodes = AST_node.sub_nodes[1..];
                int if_instruction_counter = 0;
                if (if_sub_nodes.Count > 0){
                    to_IR(if_sub_nodes[0], id_infos, sb_new, ref temp_push_count, ref if_instruction_counter);
                    foreach ((int i, Node AST_sub_node) in if_sub_nodes[1..].Index())
                        to_IR(AST_sub_node, id_infos, sb_new, ref temp_push_count, ref if_instruction_counter);
                }
                sb.add_instruction($"JMPZ {if_instruction_counter + 1}");
                ++instruction_counter;
                sb.Append(sb_new);
                foreach (string key in id_infos.Keys.ToList()){
                    if (id_infos[key].nest_count == 1){
                        id_infos.Remove(key);
                        sb.add_instruction("POP");
                        ++instruction_counter;
                    }
                }
                break;
            case Token.Type.ELSE:
                foreach (string key in id_infos.Keys.ToList()){
                    Id id = id_infos[key];
                    ++id.nest_count;
                    id_infos[key] = id;
                }
                int else_instruction_counter = 0;
                List<Node> else_sub_nodes = AST_node.sub_nodes;
                if (else_sub_nodes.Count > 0){
                    to_IR(else_sub_nodes[0], id_infos, sb, ref temp_push_count, ref else_instruction_counter);
                    foreach ((int i, Node AST_sub_node) in else_sub_nodes[1..].Index())
                        to_IR(AST_sub_node, id_infos, sb_new, ref temp_push_count, ref else_instruction_counter);
                }
                sb.add_instruction($"JMP {else_instruction_counter + 1}");
                ++instruction_counter;
                sb.Append(sb_new);
                foreach (string key in id_infos.Keys.ToList()){
                    if (id_infos[key].nest_count == 1){
                        id_infos.Remove(key);
                        sb.add_instruction("POP");
                        ++instruction_counter;
                    }
                }
                break;

            case Token.Type.WHILE:
                break;

            case Token.Type.AND:
                sb.add_instruction("AND");
                --temp_push_count;
                ++instruction_counter;
                break;
            case Token.Type.OR:
                sb.add_instruction("OR");
                --temp_push_count;
                ++instruction_counter;
                break;
            case Token.Type.NOT:
                sb.add_instruction("NEG");
                --temp_push_count;
                ++instruction_counter;
                break;

            case Token.Type.RETURN:
                sb.add_instruction("RET");
                --temp_push_count;
                ++instruction_counter;
                break;
        }
    }

    public static string to_IR(List<Node> AST){
        Dictionary<string, Id> id_infos = new();

        StringBuilder sb = new();

        int temp_push_count = 0;
        int instruction_counter = 0;

        to_IR(AST[0], id_infos, sb, ref temp_push_count, ref instruction_counter);
        foreach ((int i, Node AST_node) in AST[1..].Index()){
            instruction_counter = 0;
            to_IR(AST_node, id_infos, sb, ref temp_push_count, ref instruction_counter);
            sb.AppendLine();
        }

        return sb.ToString();
    }
    public static string to_IR(List<Token> tokens) => to_IR(Parser.build_AST(tokens));
    public static string to_IR(string path) => to_IR(Lexer.tokenize(path));
}
