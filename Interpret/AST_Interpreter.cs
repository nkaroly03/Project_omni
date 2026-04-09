namespace Interpret;

using Lex;
using Parse;
using System.Diagnostics;
using System.Globalization;

public sealed class AST_Interpreter{
    Dictionary<string, Value> m_ids;
    Dictionary<string, int> m_id_counts;
    List<Node> m_AST;

    void increment_id_counts(){
        List<string> keys = m_ids.Keys.ToList();
        foreach (string k in keys)
            ++m_id_counts[k];
    }
    void decrement_id_counts(){
        List<string> keys = m_ids.Keys.ToList();
        foreach (string k in keys)
            --m_id_counts[k];
        foreach (string k in keys){
            if (m_id_counts[k] == 0){
                m_ids.Remove(k);
                m_id_counts.Remove(k);
            }
        }
    }

    Value calculate_expr(Node node){
        Value v1, v2;

        switch (node.token.type){
            case Token.Type.ID:
                return new(m_ids[node.token.id]);
            case Token.Type.FALSE:
                return new(false);
            case Token.Type.TRUE:
                return new(true);
            case Token.Type.INT_LIT:
                return new(Convert.ToInt32(node.token.id));
            case Token.Type.FLOAT_LIT:
                return new(Convert.ToSingle(node.token.id, CultureInfo.InvariantCulture));

            case Token.Type.LESS_THAN:
            case Token.Type.LESS_THAN_EQ:
            case Token.Type.GREATER_THAN:
            case Token.Type.GREATER_THAN_EQ:
            case Token.Type.NOT_EQUALS:
            case Token.Type.EQUALS:
            case Token.Type.ASTERISK:
            case Token.Type.SLASH:
            case Token.Type.PERCENT:
                v1 = calculate_expr(node.sub_nodes[0]);
                v2 = calculate_expr(node.sub_nodes[1]);
                switch (node.token.type){
                    case Token.Type.LESS_THAN:       return new(v1 <  v2);
                    case Token.Type.LESS_THAN_EQ:    return new(v1 <= v2);
                    case Token.Type.GREATER_THAN:    return new(v1 >  v2);
                    case Token.Type.GREATER_THAN_EQ: return new(v1 >= v2);
                    case Token.Type.NOT_EQUALS:      return new(v1 != v2);
                    case Token.Type.EQUALS:          return new(v1 == v2);

                    case Token.Type.ASTERISK:        return v1 * v2;
                    case Token.Type.SLASH:           return v1 / v2;
                    case Token.Type.PERCENT:         return v1 % v2;

                    default:                         throw new UnreachableException();
                }

            case Token.Type.PLUS:
            case Token.Type.MINUS:
                v1 = calculate_expr(node.sub_nodes[0]);
                if (node.sub_nodes.Count > 1){
                    v2 = calculate_expr(node.sub_nodes[1]);
                    return (node.token.type == Token.Type.PLUS) ? new(v1 + v2) : new(v1 - v2);
                }
                else
                    return (node.token.type == Token.Type.PLUS) ? v1 : -v1;

            case Token.Type.SCAN:
                Console.WriteLine(node.sub_nodes[0].token.id);
                string s = Console.ReadLine()!;
                try{ return new(Convert.ToBoolean(s)); }
                catch (FormatException){
                    try{ return new(Convert.ToInt32(s)); }
                    catch (FormatException){ return new(Convert.ToSingle(s, CultureInfo.InvariantCulture)); }
                }

            case Token.Type.AND:
                return new(calculate_expr(node.sub_nodes[0]).to_bool() && calculate_expr(node.sub_nodes[1]).to_bool());
            case Token.Type.OR:
                return new(calculate_expr(node.sub_nodes[0]).to_bool() || calculate_expr(node.sub_nodes[1]).to_bool());
            case Token.Type.NOT:
                return new(!calculate_expr(node.sub_nodes[0]).to_bool());

            default:
                throw new UnreachableException();
        }
    }

    Value run(List<Node> nodes, ref Value? result){
        bool if_statement_ran = false;
        foreach (Node node in nodes){
            if (result is not null)
                break;
            switch (node.token.type){
                case Token.Type.ID:
                    break;
                case Token.Type.LET_DECL:
                    Value v;
                    switch (node.sub_nodes[1].token.type){
                        case Token.Type.BOOL:
                            v = new Value(calculate_expr(node.sub_nodes[1].sub_nodes[0]).to_bool());
                            break;
                        case Token.Type.INT:
                            v = new Value(calculate_expr(node.sub_nodes[1].sub_nodes[0]).to_int());
                            break;
                        case Token.Type.FLOAT:
                            v = new Value(calculate_expr(node.sub_nodes[1].sub_nodes[0]).to_float());
                            break;
                        default:
                            throw new UnreachableException();
                    }
                    m_ids.Add(node.sub_nodes[0].token.id, v);
                    m_id_counts.Add(node.sub_nodes[0].token.id, 1);
                    break;
                case Token.Type.EQ:
                    m_ids[node.sub_nodes[0].token.id] = calculate_expr(node.sub_nodes[1]);
                    break;
                case Token.Type.PRINT:
                    Console.WriteLine((node.sub_nodes[0].token.type == Token.Type.STR_LIT) ? node.sub_nodes[0].token.id : calculate_expr(node.sub_nodes[0]));
                    break;
                case Token.Type.IF:
                    bool if_condition = calculate_expr(node.sub_nodes[0]).to_bool();
                    if (if_condition){
                        increment_id_counts();
                        if_statement_ran = true;
                        run(node.sub_nodes[1..], ref result);
                        decrement_id_counts();
                    }
                    break;
                case Token.Type.ELSE:
                    if (!if_statement_ran){
                        increment_id_counts();
                        run(node.sub_nodes, ref result);
                        decrement_id_counts();
                    }
                    if_statement_ran = false;
                    break;
                case Token.Type.WHILE:
                    bool while_condition = calculate_expr(node.sub_nodes[0]).to_bool();
                    while (while_condition && result is null){
                        increment_id_counts();
                        run(node.sub_nodes[1..], ref result);
                        while_condition = calculate_expr(node.sub_nodes[0]).to_bool();
                        decrement_id_counts();
                    }
                    break;
                case Token.Type.RETURN:
                    result = calculate_expr(node.sub_nodes[0]);
                    break;

                default:
                    throw new Exception($"On line <{node.token.line_number}> found invalid token <{node.token.id}>".colour_str());
            }
        }

        return result!;
    }

    public AST_Interpreter(List<Node> AST) => (m_ids, m_id_counts, m_AST) = (new(), new(), AST);
    public AST_Interpreter(List<Token> tokens) : this(Parser.build_AST(tokens)){}
    public AST_Interpreter(string path) : this(Lexer.tokenize(path)){}

    public Value run(){
        Value? temp = null;

        temp = run(m_AST, ref temp);

        m_ids = new();
        m_id_counts = new();

        return temp;
    }
}
