namespace DaScreenshotStudio.Assets.Expressions;

public enum ExpressionValueKind
{
    Number,
    Boolean,
}

public sealed class ExpressionProgram
{
    public const int MaxStackDepth = 256;

    public const int MaxNodeDepth = 256;

    internal enum Op
    {
        PushConstant,
        PushNumber,
        PushBoolean,
        Add,
        Subtract,
        Multiply,
        Divide,
        Equal,
        Less,
        Greater,
        Not,

        BranchIfFalse,
        Branch,

        AndBranch,

        OrBranch,
    }

    private readonly int[] _code;
    private readonly double[] _constants;
    private readonly int _stackDepth;

    internal ExpressionProgram(int[] code, double[] constants, int stackDepth, ExpressionValueKind kind, string source)
    {
        _code = code;
        _constants = constants;
        _stackDepth = stackDepth;
        Kind = kind;
        Source = source;
    }

    public ExpressionValueKind Kind { get; }

    public string Source { get; }

    public double Evaluate(ReadOnlySpan<double> numbers, ReadOnlySpan<bool> booleans)
    {
        Span<double> stack = stackalloc double[_stackDepth];
        var top = 0;
        var code = _code;

        for (var ip = 0; ip < code.Length;)
        {
            var op = (Op)code[ip];
            var argument = code[ip + 1];
            ip += 2;

            switch (op)
            {
                case Op.PushConstant:
                    stack[top++] = _constants[argument];
                    break;
                case Op.PushNumber:
                    stack[top++] = numbers[argument];
                    break;
                case Op.PushBoolean:
                    stack[top++] = booleans[argument] ? 1 : 0;
                    break;
                case Op.Add:
                    top--;
                    stack[top - 1] += stack[top];
                    break;
                case Op.Subtract:
                    top--;
                    stack[top - 1] -= stack[top];
                    break;
                case Op.Multiply:
                    top--;
                    stack[top - 1] *= stack[top];
                    break;
                case Op.Divide:
                    top--;
                    stack[top - 1] /= stack[top];
                    break;
                case Op.Equal:
                    top--;
                    stack[top - 1] = stack[top - 1] == stack[top] ? 1 : 0;
                    break;
                case Op.Less:
                    top--;
                    stack[top - 1] = stack[top - 1] < stack[top] ? 1 : 0;
                    break;
                case Op.Greater:
                    top--;
                    stack[top - 1] = stack[top - 1] > stack[top] ? 1 : 0;
                    break;
                case Op.Not:
                    stack[top - 1] = stack[top - 1] != 0 ? 0 : 1;
                    break;
                case Op.BranchIfFalse:
                    top--;
                    if (stack[top] == 0)
                    {
                        ip = argument;
                    }

                    break;
                case Op.Branch:
                    ip = argument;
                    break;
                case Op.AndBranch:
                    if (stack[top - 1] == 0)
                    {
                        ip = argument;
                    }
                    else
                    {
                        top--;
                    }

                    break;
                default:
                    if (stack[top - 1] != 0)
                    {
                        ip = argument;
                    }
                    else
                    {
                        top--;
                    }

                    break;
            }
        }

        return stack[0];
    }
}
