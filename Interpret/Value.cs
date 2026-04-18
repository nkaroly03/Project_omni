namespace Interpret;

using System.Diagnostics;

public sealed class Value{
    static Value arithm_op(Value v1, Value v2, Action<Value, Value> op){
        Value temp = v1;

        switch (v1.data){
            case bool:
                switch (v2.data){
                    case int:
                        temp.data = v1.to_int();
                        break;
                    case float:
                        temp.data = v1.to_float();
                        break;
                }
                break;
            case int:
                if (v2.data is float)
                    temp.data = v1.to_float();
                break;
        }

        op(temp, v2);

        return temp;
    }
    static Value bitwise_arithm_op(Value v1, Value v2, Action<Value, Value> op){
        op(v1, v2);

        return v1;
    }

    static bool cmp_op(Value v1, Value v2, Func<int, int, bool> int_cmp, Func<float, float, bool> float_cmp){
        return v1.data switch{
            bool or int => v2.data switch{
                bool or int => int_cmp(v1.to_int(), v2.to_int()),
                float       => float_cmp(v1.to_float(), v2.to_float()),
                _           => throw new UnreachableException(),
            },
            float => float_cmp(v1.to_float(), v2.to_float()),

            _ => throw new UnreachableException(),
        };
    }

    public static Value operator+(Value v) => new(v);
    public static Value operator-(Value v) => v.data switch{
        bool  => new(!(bool)v.data),
        int   => new(-(int)v.data),
        float => new(-(float)v.data),

        _ => throw new UnreachableException(),
    };

    public static Value operator+(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.add_eq(_v2));
    public static Value operator-(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.sub_eq(_v2));
    public static Value operator*(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.mul_eq(_v2));
    public static Value operator/(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.div_eq(_v2));
    public static Value operator%(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.mod_eq(_v2));
    public static Value       pow(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.pow_eq(_v2));

    public static Value operator<<(Value v1, Value v2) => bitwise_arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.shl_eq (_v2));
    public static Value operator>>(Value v1, Value v2) => bitwise_arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.shr_eq (_v2));
    public static Value operator& (Value v1, Value v2) => bitwise_arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.band_eq(_v2));
    public static Value operator| (Value v1, Value v2) => bitwise_arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.bor_eq (_v2));
    public static Value operator^ (Value v1, Value v2) => bitwise_arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.xor_eq (_v2));
    public static Value operator~ (Value v) => v.data switch{
        int => new(~v.to_int()),

        _ => throw new InvalidOperationException("Trying to use bitwise operations on non integer types"),
    };

    public static bool operator==(Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 == i2), (f1, f2) => (f1 == f2));
    public static bool operator!=(Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 != i2), (f1, f2) => (f1 != f2));
    public static bool operator< (Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 <  i2), (f1, f2) => (f1 <  f2));
    public static bool operator<=(Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 <= i2), (f1, f2) => (f1 <= f2));
    public static bool operator> (Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 >  i2), (f1, f2) => (f1 >  f2));
    public static bool operator>=(Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 >= i2), (f1, f2) => (f1 >= f2));

    public object data{ get; private set; }

    public Value(bool data)  => this.data = data;
    public Value(int data)   => this.data = data;
    public Value(float data) => this.data = data;

    public Value(Value other) => data = other.data;

    public override string ToString() => data.ToString()!;

    public override bool Equals(object? obj) => obj is Value && this == (Value)obj;

    public override int GetHashCode() => data.GetHashCode();

    public bool to_bool() => data switch{
        bool  => (bool)data,
        int   => Convert.ToBoolean(data),
        float => Convert.ToBoolean(data),

        _ => throw new UnreachableException()
    };
    public int to_int() => data switch{
        bool  => Convert.ToInt32(data),
        int   => (int)data,
        float => (int)((float)data),

        _ => throw new UnreachableException()
    };
    public float to_float() => data switch{
        bool  => Convert.ToSingle(data),
        int   => (float)((int)data),
        float => (float)data,

        _ => throw new UnreachableException()
    };

    public void add_eq(Value v) => data = data switch{
        bool  => (bool)data || v.to_bool(),
        int   => (int)data + v.to_int(),
        float => (float)data + v.to_float(),

        _ => throw new UnreachableException()
    };
    public void sub_eq(Value v) => data = data switch{
        bool  => Convert.ToBoolean(to_int() - v.to_int()),
        int   => (int)data - v.to_int(),
        float => (float)data - v.to_float(),

        _ => throw new UnreachableException()
    };
    public void mul_eq(Value v) => data = data switch{
        bool  => (bool)data && v.to_bool(),
        int   => (int)data * v.to_int(),
        float => (float)data * v.to_float(),

        _ => throw new UnreachableException()
    };
    public void div_eq(Value v) => data = data switch{
        bool  => Convert.ToBoolean(to_int() / v.to_int()),
        int   => (int)data / v.to_int(),
        float => (float)data / v.to_float(),

        _ => throw new UnreachableException()
    };
    public void mod_eq(Value v) => data = data switch{
        bool  => Convert.ToBoolean(to_int() % v.to_int()),
        int   => (int)data % v.to_int(),
        float => (float)data % v.to_float(),

        _ => throw new UnreachableException()
    };
    public void pow_eq(Value v){
        float result = MathF.Pow(to_float(), v.to_float());
        data = data switch{
            bool  => Convert.ToBoolean(result),
            int   => (int)result,
            float => result,

            _ => throw new UnreachableException()
        };
    }

    public void shl_eq(Value v){
        if (data is not int || v.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() << v.to_int();
    }
    public void shr_eq(Value v){
        if (data is not int || v.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() >> v.to_int();
    }
    public void band_eq(Value v){
        if (data is not int || v.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() & v.to_int();
    }
    public void bor_eq(Value v){
        if (data is not int || v.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() | v.to_int();
    }
    public void xor_eq(Value v){
        if (data is not int || v.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() ^ v.to_int();
    }
}
