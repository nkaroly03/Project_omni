namespace Interpret;

using System.Diagnostics;

public sealed class Value : IEquatable<Value>, IComparable<Value>{
    static Value arithm_op(Value v1, Value v2, Action<Value, Value> op){
        v1.data = v1.data switch{
            bool b => v2.data switch{
                bool  => v1.data,
                int   => Convert.ToInt32(b),
                float => Convert.ToSingle(b),

                _ => throw new UnreachableException(),
            },
            int  i => v2.data switch{
                bool or int => v1.data,
                float       => (float)i,

                _ => throw new UnreachableException(),
            },
            float  => v1.data,

            _ => throw new UnreachableException(),
        };

        op(v1, v2);

        return v1;
    }
    static Value barithm_op(Value v1, Value v2, Action<Value, Value> op){
        op(v1, v2);

        return v1;
    }

    public static Value operator+(Value v) => new(v);
    public static Value operator-(Value v) => v.data switch{
        bool  b => new(!b),
        int   i => new(-i),
        float f => new(-f),

        _ => throw new UnreachableException(),
    };

    public static Value operator+(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.add_eq(_v2));
    public static Value operator-(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.sub_eq(_v2));
    public static Value operator*(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.mul_eq(_v2));
    public static Value operator/(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.div_eq(_v2));
    public static Value operator%(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.mod_eq(_v2));
    public static Value       pow(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.pow_eq(_v2));

    public static Value operator<<(Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1. shl_eq(_v2));
    public static Value operator>>(Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1. shr_eq(_v2));
    public static Value operator& (Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1.band_eq(_v2));
    public static Value operator| (Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1. bor_eq(_v2));
    public static Value operator^ (Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1. xor_eq(_v2));
    public static Value operator~ (Value v) => new((v.data is int) ? ~v.to_int() : throw new InvalidOperationException("Trying to use bitwise operations on non integer types"));

    public static bool operator==(Value v1, Value v2) =>  v1.Equals(v2);
    public static bool operator!=(Value v1, Value v2) => !v1.Equals(v2);
    public static bool operator< (Value v1, Value v2) =>  v1.CompareTo(v2) <  0;
    public static bool operator<=(Value v1, Value v2) =>  v1.CompareTo(v2) <= 0;
    public static bool operator> (Value v1, Value v2) =>  v1.CompareTo(v2) >  0;
    public static bool operator>=(Value v1, Value v2) =>  v1.CompareTo(v2) >= 0;

    public static Value from_str(string str){
        try{ return new(bool.Parse(str)); }
        catch (FormatException){
            try{ return new(int.Parse(str)); }
            catch (OverflowException){ return new((str.Trim()[0] == '-') ? int.MinValue : int.MaxValue); }
            catch (FormatException){
                try{ return new(float.Parse(str, System.Globalization.CultureInfo.InvariantCulture)); }
                catch (OverflowException){ return new((str.Trim()[0] == '-') ? float.MinValue : float.MaxValue); }
            }
        }
    }

    public object data{ get; private set; }

    public Value(bool data)  => this.data = data;
    public Value(int data)   => this.data = data;
    public Value(float data) => this.data = data;

    public Value(Value other) => data = other.data;

    public override string ToString() => data.ToString()!;
    public override bool Equals(object? obj) => Equals(obj as Value);
    public override int GetHashCode() => data.GetHashCode();

    public bool Equals(Value? other) => (other is not null)
        ? ((data is float || other.data is float) ? to_float().Equals(other.to_float()) : to_int().Equals(other.to_int()))
        : false
    ;
    public int CompareTo(Value? other) => (other is not null)
        ? ((data is float || other.data is float) ? to_float().CompareTo(other.to_float()) : to_int().CompareTo(other.to_int()))
        : 1
    ;

    public bool to_bool() => data switch{
        bool  b => b,
        int   i => Convert.ToBoolean(i),
        float f => Convert.ToBoolean(f),

        _ => throw new UnreachableException()
    };
    public int to_int() => data switch{
        bool  b => Convert.ToInt32(b),
        int   i => i,
        float f => (int)f,

        _ => throw new UnreachableException()
    };
    public float to_float() => data switch{
        bool  b => Convert.ToSingle(b),
        int   i => (float)i,
        float f => f,

        _ => throw new UnreachableException()
    };

    public void add_eq(Value other) => data = data switch{
        bool  b => b || other.to_bool(),
        int   i => i + other.to_int(),
        float f => f + other.to_float(),

        _ => throw new UnreachableException()
    };
    public void sub_eq(Value other) => data = data switch{
        bool  b => Convert.ToBoolean(Convert.ToInt32(b) - other.to_int()),
        int   i => i - other.to_int(),
        float f => f - other.to_float(),

        _ => throw new UnreachableException()
    };
    public void mul_eq(Value other) => data = data switch{
        bool  b => b && other.to_bool(),
        int   i => i * other.to_int(),
        float f => f * other.to_float(),

        _ => throw new UnreachableException()
    };
    public void div_eq(Value other) => data = data switch{
        bool  b => Convert.ToBoolean(Convert.ToInt32(b) / other.to_int()),
        int   i => i / other.to_int(),
        float f => f / other.to_float(),

        _ => throw new UnreachableException()
    };
    public void mod_eq(Value other) => data = data switch{
        bool  b => Convert.ToBoolean(Convert.ToInt32(b) % other.to_int()),
        int   i => i % other.to_int(),
        float f => f % other.to_float(),

        _ => throw new UnreachableException()
    };
    public void pow_eq(Value other){
        float result = MathF.Pow(to_float(), other.to_float());
        data = data switch{
            bool  => Convert.ToBoolean(result),
            int   => (int)result,
            float => result,

            _ => throw new UnreachableException()
        };
    }

    public void shl_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() << other.to_int();
    }
    public void shr_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() >> other.to_int();
    }
    public void band_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() & other.to_int();
    }
    public void bor_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() | other.to_int();
    }
    public void xor_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non integer types");

        data = to_int() ^ other.to_int();
    }
}
