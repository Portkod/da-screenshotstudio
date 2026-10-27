using System.Globalization;
using System.Text;

namespace DaScreenshotStudio.Assets.Expressions;

public sealed class ExpressionContext
{
    public const int MaxCompiledExpressions = 1024;

    public const int VariableCount = 256;

    private readonly double[] _numbers = new double[VariableCount];
    private readonly bool[] _booleans = new bool[VariableCount];
    private readonly bool[] _numberRegistered = new bool[VariableCount];
    private readonly bool[] _booleanRegistered = new bool[VariableCount];
    private readonly List<ExpressionProgram> _compiled = [];

    public ExpressionProgram this[int handle] => _compiled[handle];

    public void RegisterNumber(byte key, double value = 0)
    {
        _numberRegistered[key] = true;
        _numbers[key] = value;
    }

    public void SetNumber(byte key, double value)
    {
        if (_numberRegistered[key])
        {
            _numbers[key] = value;
        }
    }

    public double Evaluate(int handle) => _compiled[handle].Evaluate(_numbers, _booleans);

    public int Compile(ReadOnlySpan<byte> source)
    {
        if (_compiled.Count >= MaxCompiledExpressions)
        {
            throw new InvalidOperationException(
                $"An expression context holds at most {MaxCompiledExpressions} compiled expressions.");
        }

        var program = Build(source);
        _compiled.Add(program);
        return _compiled.Count - 1;
    }

    public bool TryCompile(ReadOnlySpan<byte> source, out int handle)
    {
        try
        {
            handle = Compile(source);
            return true;
        }
        catch (InvalidDataException)
        {
            handle = -1;
            return false;
        }
    }

    private ExpressionProgram Build(ReadOnlySpan<byte> source)
    {
        var nodes = new List<Node>();
        var operators = new List<int>();
        var operands = new List<int>();

        operators.Add(ExpressionOperators.OpenParenthesis);

        var tokenizer = new ExpressionTokenizer(source);
        var finished = false;

        while (true)
        {
            ReadOnlySpan<byte> token;
            if (tokenizer.TryReadToken(out token))
            {
                if (TryParseNumber(token, out var literal))
                {
                    operands.Add(AddNode(nodes, Node.Constant(literal)));
                    continue;
                }
            }
            else
            {
                if (finished)
                {
                    break;
                }

                finished = true;
                token = ")"u8;
            }

            if (ExpressionOperators.TryFind(token, out var id))
            {
                Reduce(nodes, operators, operands, id, source);
                continue;
            }

            if (token.Length == 1)
            {
                var key = token[0];
                if (!_numberRegistered[key])
                {
                    throw Malformed(source, $"names the unregistered numeric variable '{(char)key}'");
                }

                operands.Add(AddNode(nodes, Node.Number(key)));
                continue;
            }

            if (token.Length == 2 && token[0] == (byte)'_')
            {
                var key = token[1];
                if (!_booleanRegistered[key])
                {
                    throw Malformed(source, $"names the unregistered Boolean variable '_{(char)key}'");
                }

                operands.Add(AddNode(nodes, Node.Boolean(key)));
                continue;
            }

            throw Malformed(source, $"holds the unknown token '{Text(token)}'");
        }

        if (operands.Count != 1)
        {
            throw Malformed(source, $"left {operands.Count} values on the stack instead of one");
        }

        var root = operands[0];
        if (nodes[root].Type != ExpressionValueKind.Number)
        {
            throw Malformed(source, "produces a Boolean, and a compiled root has to be a number");
        }

        return Emit(nodes, root, Text(source));
    }

    private static void Reduce(
        List<Node> nodes, List<int> operators, List<int> operands, int incoming, ReadOnlySpan<byte> source)
    {
        ref readonly var token = ref ExpressionOperators.Get(incoming);

        while (true)
        {
            if (operators.Count == 0)
            {
                throw Malformed(source, "closes more groups than it opens");
            }

            var top = operators[^1];
            if (top == token.Partner)
            {
                operators.RemoveAt(operators.Count - 1);

                if (incoming == ExpressionOperators.Colon)
                {
                    operators.Add(incoming);
                }

                return;
            }

            ref readonly var stacked = ref ExpressionOperators.Get(top);
            if (stacked.StackPriority > token.IncomingPriority)
            {
                operators.Add(incoming);
                return;
            }

            if (operands.Count < stacked.Arity)
            {
                throw Malformed(source, $"gives '{(char)stacked.Token}' only {operands.Count} operands");
            }

            var first = operands[^1];
            operands.RemoveAt(operands.Count - 1);
            var second = stacked.Arity > 1 ? operands[^1] : -1;
            if (stacked.Arity > 1)
            {
                operands.RemoveAt(operands.Count - 1);
            }

            var third = stacked.Arity > 2 ? operands[^1] : -1;
            if (stacked.Arity > 2)
            {
                operands.RemoveAt(operands.Count - 1);
            }

            operands.Add(AddNode(nodes, BuildOperator(nodes, top, first, second, third, source)));
            operators.RemoveAt(operators.Count - 1);
        }
    }

    private static Node BuildOperator(
        List<Node> nodes, int id, int first, int second, int third, ReadOnlySpan<byte> source)
    {
        switch (id)
        {
            case ExpressionOperators.Colon:
                {
                    Expect(nodes, third, ExpressionValueKind.Boolean, source, "a conditional's condition");
                    var kind = nodes[second].Type;
                    Expect(nodes, first, kind, source, "both conditional branches");
                    return Node.Conditional(third, second, first, kind);
                }

            case ExpressionOperators.Equal:
                {
                    var kind = nodes[second].Type;
                    Expect(nodes, first, kind, source, "both sides of '='");
                    return Node.Binary(id, second, first, ExpressionValueKind.Boolean);
                }

            case ExpressionOperators.Less:
            case ExpressionOperators.Greater:
                Expect(nodes, second, ExpressionValueKind.Number, source, "both sides of a comparison");
                Expect(nodes, first, ExpressionValueKind.Number, source, "both sides of a comparison");
                return Node.Binary(id, second, first, ExpressionValueKind.Boolean);

            case ExpressionOperators.Not:
                Expect(nodes, first, ExpressionValueKind.Boolean, source, "the operand of '!'");
                return Node.Unary(id, first);

            case ExpressionOperators.Add:
            case ExpressionOperators.Subtract:
            case ExpressionOperators.Multiply:
            case ExpressionOperators.Divide:
                Expect(nodes, second, ExpressionValueKind.Number, source, "both sides of an arithmetic operator");
                Expect(nodes, first, ExpressionValueKind.Number, source, "both sides of an arithmetic operator");
                return Node.Binary(id, second, first, ExpressionValueKind.Number);

            case ExpressionOperators.And:
            case ExpressionOperators.Or:
                Expect(nodes, second, ExpressionValueKind.Boolean, source, "both sides of a Boolean operator");
                Expect(nodes, first, ExpressionValueKind.Boolean, source, "both sides of a Boolean operator");
                return Node.Binary(id, second, first, ExpressionValueKind.Boolean);

            default:
                throw Malformed(source, $"reduces '{(char)ExpressionOperators.Get(id).Token}', which builds no node");
        }
    }

    private static void Expect(
        List<Node> nodes, int node, ExpressionValueKind kind, ReadOnlySpan<byte> source, string what)
    {
        if (node < 0 || nodes[node].Type != kind)
        {
            throw Malformed(source, $"needs {what} to be {(kind == ExpressionValueKind.Number ? "numeric" : "Boolean")}");
        }
    }

    private static int AddNode(List<Node> nodes, Node node)
    {
        nodes.Add(node);
        return nodes.Count - 1;
    }

    private static ExpressionProgram Emit(List<Node> nodes, int root, string source)
    {
        var code = new List<int>();
        var constants = new List<double>();
        var depth = 0;
        var maxDepth = 0;

        EmitNode(nodes, root, code, constants, ref depth, ref maxDepth, 0);

        if (maxDepth > ExpressionProgram.MaxStackDepth)
        {
            throw new InvalidDataException(
                $"An expression needs a stack {maxDepth} deep, more than the {ExpressionProgram.MaxStackDepth} allowed.");
        }

        return new ExpressionProgram([.. code], [.. constants], maxDepth, nodes[root].Type, source);
    }

    private static void EmitNode(
        List<Node> nodes,
        int index,
        List<int> code,
        List<double> constants,
        ref int depth,
        ref int maxDepth,
        int nesting)
    {
        if (nesting > ExpressionProgram.MaxNodeDepth)
        {
            throw new InvalidDataException(
                $"An expression nests operators more than {ExpressionProgram.MaxNodeDepth} deep.");
        }

        var node = nodes[index];

        switch (node.Kind)
        {
            case NodeKind.Constant:
                constants.Add(node.Value);
                Write(code, ExpressionProgram.Op.PushConstant, constants.Count - 1);
                Push(ref depth, ref maxDepth);
                return;

            case NodeKind.Number:
                Write(code, ExpressionProgram.Op.PushNumber, node.Key);
                Push(ref depth, ref maxDepth);
                return;

            case NodeKind.Boolean:
                Write(code, ExpressionProgram.Op.PushBoolean, node.Key);
                Push(ref depth, ref maxDepth);
                return;

            case NodeKind.Conditional:
                {
                    EmitNode(nodes, node.A, code, constants, ref depth, ref maxDepth, nesting + 1);

                    var branch = Write(code, ExpressionProgram.Op.BranchIfFalse, 0);
                    depth--;
                    var settled = depth;

                    EmitNode(nodes, node.B, code, constants, ref depth, ref maxDepth, nesting + 1);
                    var jump = Write(code, ExpressionProgram.Op.Branch, 0);
                    code[branch + 1] = code.Count;

                    depth = settled;
                    EmitNode(nodes, node.C, code, constants, ref depth, ref maxDepth, nesting + 1);
                    code[jump + 1] = code.Count;
                    return;
                }

            default:
                break;
        }

        if (node.Operator is ExpressionOperators.And or ExpressionOperators.Or)
        {
            EmitNode(nodes, node.A, code, constants, ref depth, ref maxDepth, nesting + 1);

            var op = node.Operator == ExpressionOperators.And
                ? ExpressionProgram.Op.AndBranch
                : ExpressionProgram.Op.OrBranch;
            var branch = Write(code, op, 0);
            depth--;

            EmitNode(nodes, node.B, code, constants, ref depth, ref maxDepth, nesting + 1);
            code[branch + 1] = code.Count;
            return;
        }

        EmitNode(nodes, node.A, code, constants, ref depth, ref maxDepth, nesting + 1);

        if (node.Operator == ExpressionOperators.Not)
        {
            Write(code, ExpressionProgram.Op.Not, 0);
            return;
        }

        EmitNode(nodes, node.B, code, constants, ref depth, ref maxDepth, nesting + 1);
        Write(code, OpFor(node.Operator), 0);
        depth--;
    }

    private static ExpressionProgram.Op OpFor(int id) => id switch
    {
        ExpressionOperators.Add => ExpressionProgram.Op.Add,
        ExpressionOperators.Subtract => ExpressionProgram.Op.Subtract,
        ExpressionOperators.Multiply => ExpressionProgram.Op.Multiply,
        ExpressionOperators.Divide => ExpressionProgram.Op.Divide,
        ExpressionOperators.Equal => ExpressionProgram.Op.Equal,
        ExpressionOperators.Less => ExpressionProgram.Op.Less,
        _ => ExpressionProgram.Op.Greater,
    };

    private static int Write(List<int> code, ExpressionProgram.Op op, int argument)
    {
        var at = code.Count;
        code.Add((int)op);
        code.Add(argument);
        return at;
    }

    private static void Push(ref int depth, ref int maxDepth)
    {
        depth++;
        if (depth > maxDepth)
        {
            maxDepth = depth;
        }
    }

    private static bool TryParseNumber(ReadOnlySpan<byte> token, out double value)
    {
        value = 0;
        if (token.IsEmpty)
        {
            return false;
        }

        var periods = 0;
        for (var i = 0; i < token.Length; i++)
        {
            var current = token[i];
            if (current == (byte)'.')
            {
                periods++;
                continue;
            }

            if (current < (byte)'0' || current > (byte)'9')
            {
                return false;
            }
        }

        return periods <= 1
            && double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string Text(ReadOnlySpan<byte> value) => Encoding.ASCII.GetString(value).Trim();

    private static InvalidDataException Malformed(ReadOnlySpan<byte> source, string reason) =>
        new($"The expression '{Text(source)}' {reason}.");

    private enum NodeKind
    {
        Constant,
        Number,
        Boolean,
        Operator,
        Conditional,
    }

    private readonly struct Node
    {
        private Node(NodeKind kind, ExpressionValueKind type, int op, double value, int key, int a, int b, int c)
        {
            Kind = kind;
            Type = type;
            Operator = op;
            Value = value;
            Key = key;
            A = a;
            B = b;
            C = c;
        }

        public NodeKind Kind { get; }

        public ExpressionValueKind Type { get; }

        public int Operator { get; }

        public double Value { get; }

        public int Key { get; }

        public int A { get; }

        public int B { get; }

        public int C { get; }

        public static Node Constant(double value) =>
            new(NodeKind.Constant, ExpressionValueKind.Number, -1, value, 0, -1, -1, -1);

        public static Node Number(int key) =>
            new(NodeKind.Number, ExpressionValueKind.Number, -1, 0, key, -1, -1, -1);

        public static Node Boolean(int key) =>
            new(NodeKind.Boolean, ExpressionValueKind.Boolean, -1, 0, key, -1, -1, -1);

        public static Node Unary(int op, int operand) =>
            new(NodeKind.Operator, ExpressionValueKind.Boolean, op, 0, 0, operand, -1, -1);

        public static Node Binary(int op, int left, int right, ExpressionValueKind type) =>
            new(NodeKind.Operator, type, op, 0, 0, left, right, -1);

        public static Node Conditional(int condition, int whenTrue, int whenFalse, ExpressionValueKind type) =>
            new(NodeKind.Conditional, type, ExpressionOperators.Colon, 0, 0, condition, whenTrue, whenFalse);
    }
}
