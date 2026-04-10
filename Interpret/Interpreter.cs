namespace Interpret;

using Compile;
using System.Globalization;
using System.Text;

static class Interpreter_extensions{
    extension(List<Value> self){
        public Value pop(){
            Value temp = self.Last();

            self.RemoveAt(self.Count - 1);

            return temp;
        }
    }
}

public static class Interpreter{
    public static Value run(byte[] bytecode){
        List<Value> stack = new();
        
        for (int pc = 0; pc < bytecode.Length;){
            switch ((Compiler.Op_code)bytecode[pc++]){
                case Compiler.Op_code.PUSH_FROM_SP:
                    stack.Add(new(stack[stack.Count + BitConverter.ToInt32(bytecode[pc..][..sizeof(int)])]));
                    pc += sizeof(int);
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
                    stack[stack.Count + BitConverter.ToInt32(bytecode[pc..][..sizeof(int)])] = stack.Last();
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
                    Console.WriteLine(print_str);
                    break;
                case Compiler.Op_code.PRINT:
                    Console.WriteLine(stack.pop());
                    break;

                case Compiler.Op_code.SCAN:
                    int scan_strlen = BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    pc += sizeof(int);
                    string scan_str = Encoding.UTF8.GetString(bytecode[pc..][..scan_strlen]);
                    pc += scan_strlen;
                    Console.WriteLine(scan_str);
                    string line_read = Console.ReadLine()!;
                    try{ stack.Add(new(bool.Parse(line_read))); }
                    catch (FormatException){
                        try{ stack.Add(new(int.Parse(line_read))); }
                        catch (OverflowException){ stack.Add(new((line_read.Trim()[0] == '-') ? int.MinValue : int.MaxValue)); }
                        catch (FormatException){
                            try{ stack.Add(new(float.Parse(line_read, CultureInfo.InvariantCulture))); }
                            catch (OverflowException){ stack.Add(new((line_read.Trim()[0] == '-') ? float.MinValue : float.MaxValue)); }
                        }
                    }
                    break;

                case Compiler.Op_code.TO_BOOL:
                    stack[stack.Count - 1] = new(stack.Last().to_bool());
                    break;
                case Compiler.Op_code.TO_INT:
                    stack[stack.Count - 1] = new(stack.Last().to_int());
                    break;
                case Compiler.Op_code.TO_FLOAT:
                    stack[stack.Count - 1] = new(stack.Last().to_float());
                    break;

                case Compiler.Op_code.ADD:
                    stack[stack.Count - 2] += stack.pop();
                    break;
                case Compiler.Op_code.SUB:
                    stack[stack.Count - 2] -= stack.pop();
                    break;
                case Compiler.Op_code.MUL:
                    stack[stack.Count - 2] *= stack.pop();
                    break;
                case Compiler.Op_code.DIV:
                    stack[stack.Count - 2] /= stack.pop();
                    break;
                case Compiler.Op_code.MOD:
                    stack[stack.Count - 2] %= stack.pop();
                    break;

                case Compiler.Op_code.NEG:
                    stack[stack.Count - 1] = -stack.Last();
                    break;
                case Compiler.Op_code.AND:
                    stack[stack.Count - 2] = new(stack[stack.Count - 2].to_bool() && stack.Last().to_bool());
                    stack.pop();
                    break;
                case Compiler.Op_code.OR:
                    stack[stack.Count - 2] = new(stack[stack.Count - 2].to_bool() || stack.Last().to_bool());
                    stack.pop();
                    break;

                case Compiler.Op_code.CMP_LE:
                    stack[stack.Count - 2] = new(stack[stack.Count - 2] <  stack.pop());
                    break;
                case Compiler.Op_code.CMP_LEQ:
                    stack[stack.Count - 2] = new(stack[stack.Count - 2] <= stack.pop());
                    break;
                case Compiler.Op_code.CMP_GE:
                    stack[stack.Count - 2] = new(stack[stack.Count - 2] >  stack.pop());
                    break;
                case Compiler.Op_code.CMP_GEQ:
                    stack[stack.Count - 2] = new(stack[stack.Count - 2] >= stack.pop());
                    break;
                case Compiler.Op_code.CMP_EQ:
                    stack[stack.Count - 2] = new(stack[stack.Count - 2] == stack.pop());
                    break;
                case Compiler.Op_code.CMP_NEQ:
                    stack[stack.Count - 2] = new(stack[stack.Count - 2] != stack.pop());
                    break;
            }
        }

        return stack.Last();
    }
}
