namespace DaScreenshotStudio.Assets.Expressions;

public readonly struct ExpressionOperator
{
    internal ExpressionOperator(byte token, int incomingPriority, int stackPriority, int arity, int partner)
    {
        Token = token;
        IncomingPriority = incomingPriority;
        StackPriority = stackPriority;
        Arity = arity;
        Partner = partner;
    }

    public byte Token { get; }

    public int IncomingPriority { get; }

    public int StackPriority { get; }

    public int Arity { get; }

    public int Partner { get; }
}

public static class ExpressionOperators
{
    public const int Add = 0;
    public const int Subtract = 1;
    public const int Multiply = 2;
    public const int Divide = 3;
    public const int And = 4;
    public const int Or = 5;
    public const int OpenParenthesis = 6;
    public const int CloseParenthesis = 7;
    public const int Question = 8;
    public const int Colon = 9;
    public const int Not = 10;
    public const int Equal = 11;
    public const int Less = 12;
    public const int Greater = 13;

    public const int Count = 14;

    private static readonly ExpressionOperator[] Operators =
    [
        new((byte)'+', 5, 5, 2, -1),
        new((byte)'-', 5, 5, 2, -1),
        new((byte)'*', 3, 3, 2, -1),
        new((byte)'/', 3, 3, 2, -1),
        new((byte)'&', 3, 3, 2, -1),
        new((byte)'|', 3, 3, 2, -1),
        new((byte)'(', 0, 100, 0, -1),
        new((byte)')', 10, 100, 0, OpenParenthesis),
        new((byte)'?', 10, 11, 0, -1),
        new((byte)':', 10, 10, 3, Question),
        new((byte)'!', 1, 1, 1, -1),
        new((byte)'=', 2, 2, 2, -1),
        new((byte)'<', 6, 6, 2, -1),
        new((byte)'>', 6, 6, 2, -1),
    ];

    public static ReadOnlySpan<ExpressionOperator> Table => Operators;

    public static ref readonly ExpressionOperator Get(int id) => ref Operators[id];

    public static bool TryFind(ReadOnlySpan<byte> token, out int id)
    {
        if (token.Length == 1)
        {
            for (var i = 0; i < Operators.Length; i++)
            {
                if (Operators[i].Token == token[0])
                {
                    id = i;
                    return true;
                }
            }
        }

        id = -1;
        return false;
    }
}
