namespace Interpret;

using Compile;
using System.Diagnostics;
using System.Text;

static class Interpreter_extensions{
    extension(List<Value> self){
        public Value pop(){
            Value temp = self[^1];

            self.RemoveAt(self.Count - 1);

            return temp;
        }
    }
}

public static class Interpreter{
    public static Value run(ReadOnlySpan<byte> bytecode, ReadOnlySpan<Value> argv){
        List<Value> stack = new();
        
        for (int pc = 0; pc < bytecode.Length;){
            switch ((Compiler.Op_code)bytecode[pc++]){
                case Compiler.Op_code.PUSH_FROM_SP:
                    stack.Add(new(stack[stack.Count + BitConverter.ToInt32(bytecode[pc..][..sizeof(int)])]));
                    pc += sizeof(int);
                    break;
                case Compiler.Op_code.PUSH_ARGC:
                    stack.Add(new(argv.Length));
                    break;
                case Compiler.Op_code.PUSH_FALSE:
                    stack.Add(new(false));
                    break;
                case Compiler.Op_code.PUSH_TRUE:
                    stack.Add(new(true));
                    break;
                case Compiler.Op_code.PUSH_INT:
                    stack.Add(new(BitConverter.ToInt32(bytecode[pc..][..sizeof(int)])));
                    pc += sizeof(int);
                    break;
                case Compiler.Op_code.PUSH_FLOAT:
                    stack.Add(new(BitConverter.ToSingle(bytecode[pc..][..sizeof(float)])));
                    pc += sizeof(float);
                    break;

                case Compiler.Op_code.POP:
                    stack.pop();
                    break;

                case Compiler.Op_code.RET:
                    pc = bytecode.Length;
                    break;

                case Compiler.Op_code.MOV:
                    int value_idx = stack.Count + BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    stack[value_idx] = stack[value_idx].data switch{
                        bool  => new(stack[^1].to_bool()),
                        int   => new(stack[^1].to_int()),
                        float => new(stack[^1].to_float()),

                        _ => throw new UnreachableException(),
                    };
                    pc += sizeof(int);
                    stack.pop();
                    break;

                case Compiler.Op_code.JMP:
                    int jmp_count = BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    pc += sizeof(int);
                    pc += (jmp_count + 1);
                    break;
                case Compiler.Op_code.JMPZ:
                    int jmpz_count = BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    pc += sizeof(int);
                    pc += (Convert.ToInt32(!stack.pop().to_bool()) * (jmpz_count + 1));
                    break;

                case Compiler.Op_code.PRINT_STR:
                    int print_strlen = BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    pc += sizeof(int);
                    string print_str = Encoding.UTF8.GetString(bytecode[pc..][..print_strlen]);
                    pc += print_strlen;
                    Console.Write(print_str);
                    break;
                case Compiler.Op_code.PRINT:
                    Console.Write(stack.pop());
                    break;

                case Compiler.Op_code.SCAN:
                    int scan_strlen = BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    pc += sizeof(int);
                    string scan_str = Encoding.UTF8.GetString(bytecode[pc..][..scan_strlen]);
                    pc += scan_strlen;
                    Console.Write(scan_str);
                    stack.Add(Value.from_str(Console.ReadLine()!));
                    break;
                
                case Compiler.Op_code.GET_ARGV:
                    stack[^1] = argv[(stack[^1].data is int) ? stack[^1].to_int() : throw new ArgumentOutOfRangeException("<argv> must be indexed with a Value that holds an int")];
                    break;

                case Compiler.Op_code.TO_BOOL:  stack[^1] = new(stack[^1].to_bool());  break;
                case Compiler.Op_code.TO_INT:   stack[^1] = new(stack[^1].to_int());   break;
                case Compiler.Op_code.TO_FLOAT: stack[^1] = new(stack[^1].to_float()); break;

                case Compiler.Op_code.CMP_EQ:  stack[^2] = new(stack[^2] == stack.pop()); break;
                case Compiler.Op_code.CMP_NEQ: stack[^2] = new(stack[^2] != stack.pop()); break;
                case Compiler.Op_code.CMP_LE:  stack[^2] = new(stack[^2] <  stack.pop()); break;
                case Compiler.Op_code.CMP_LEQ: stack[^2] = new(stack[^2] <= stack.pop()); break;
                case Compiler.Op_code.CMP_GE:  stack[^2] = new(stack[^2] >  stack.pop()); break;
                case Compiler.Op_code.CMP_GEQ: stack[^2] = new(stack[^2] >= stack.pop()); break;

                case Compiler.Op_code.ADD:  stack[^2]  += stack.pop(); break;
                case Compiler.Op_code.SUB:  stack[^2]  -= stack.pop(); break;
                case Compiler.Op_code.MUL:  stack[^2]  *= stack.pop(); break;
                case Compiler.Op_code.DIV:  stack[^2]  /= stack.pop(); break;
                case Compiler.Op_code.MOD:  stack[^2]  %= stack.pop(); break;
                case Compiler.Op_code.SHL:  stack[^2] <<= stack.pop(); break;
                case Compiler.Op_code.SHR:  stack[^2] >>= stack.pop(); break;
                case Compiler.Op_code.BAND: stack[^2]  &= stack.pop(); break;
                case Compiler.Op_code.BOR:  stack[^2]  |= stack.pop(); break;
                case Compiler.Op_code.XOR:  stack[^2]  ^= stack.pop(); break;
                                            
                case Compiler.Op_code.POW:
                    stack[^2] = Value.pow(stack[^2], stack.pop());
                    break;

                case Compiler.Op_code.BNEG:
                    stack[^1] = ~stack[^1];
                    break;

                // case Compiler.Op_code.AND:
                    // stack[^2] = new(stack[^2].to_bool() && stack[^1].to_bool());
                    // stack.pop();
                    // break;
                // case Compiler.Op_code.OR:
                    // stack[^2] = new(stack[^2].to_bool() || stack[^1].to_bool());
                    // stack.pop();
                    // break;
                case Compiler.Op_code.NEG:
                    stack[^1] = -stack[^1];
                    break;
            }
        }

        return stack[^1];
    }
    public static Value run(ReadOnlySpan<byte> bytecode) => run(bytecode, new());
}
