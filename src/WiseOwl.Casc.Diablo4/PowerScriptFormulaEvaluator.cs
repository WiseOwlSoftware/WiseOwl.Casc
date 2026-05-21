using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// FR-C13 Phase 2 — a small recursive-descent expression evaluator for
/// the text expressions that appear in <c>DT_STRING_FORMULA</c> slot
/// values (e.g. <c>"SF_1 / 3"</c> on Demonic Spicules) and in Power
/// description format-string placeholders (e.g.
/// <c>[SF_0*SF_1*100|%x|]</c>). The grammar supports numeric literals,
/// SF_N references (bare or braced as <c>{SF_N}</c>), the four
/// arithmetic operators (<c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>),
/// parentheses, and zero-or-more-argument function calls. Function
/// calls (<c>PlayerHealthMax()</c>, etc.) resolve through a
/// caller-supplied delegate; if none is registered the call returns
/// <see cref="double.NaN"/> and the call is appended to the collected
/// function-reference list for the caller to surface.
/// </summary>
internal static class PowerScriptFormulaEvaluator
{
    /// <summary>Result of evaluating one expression: the numeric value
    /// (<see cref="double.NaN"/> when an unresolved function call or
    /// undefined slot reference short-circuits the calculation) and the
    /// list of <see cref="PowerFunctionRef"/>s the expression contained
    /// (regardless of whether the resolver delegate consumed them).</summary>
    public readonly record struct EvaluationResult(
        double Value,
        IReadOnlyList<PowerFunctionRef> FunctionRefs);

    /// <summary>Parse and evaluate <paramref name="expression"/>. Numeric
    /// literals use invariant-culture parsing. <paramref name="slotLookup"/>
    /// returns the resolved value for an SF_N reference;
    /// <paramref name="functionResolver"/> is optional and, when present,
    /// is consulted for each function-call node — returning
    /// <see langword="null"/> indicates the consumer cannot resolve the
    /// call so the library should surface the call as an unresolved
    /// <see cref="PowerFunctionRef"/>.</summary>
    public static EvaluationResult Evaluate(
        string expression,
        Func<int, double> slotLookup,
        Func<string, IReadOnlyList<double>, double?>? functionResolver = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var tokens = Tokenize(expression);
        var parser = new Parser(tokens);
        var ast = parser.ParseExpression();
        parser.ExpectEnd();
        var refs = new List<PowerFunctionRef>();
        var ctx = new EvalContext(slotLookup, functionResolver, null, refs);
        var value = ast.Evaluate(ctx);
        return new EvaluationResult(value, refs);
    }

    /// <summary>
    /// FR-C13 Phase 3 — parse <paramref name="expression"/> and evaluate
    /// it with numeric literals substituted from
    /// <paramref name="binaryLiterals"/> in left-to-right encounter
    /// order. This is the "binary AST" evaluation path: the operator
    /// tree is built from the slot's stored text (e.g.
    /// <c>"SF_1 / 3"</c>), but each numeric literal consumed during
    /// evaluation reads its value from the compiled record's binary
    /// IEEE-754 single (the engine-truth operand bytes) instead of
    /// re-parsing the text.
    /// <br/><br/>
    /// When <paramref name="binaryLiterals"/> is <see langword="null"/>
    /// or empty, falls back to text-parsed literals — equivalent to
    /// <see cref="Evaluate(string, Func{int, double}, Func{string, IReadOnlyList{double}, double?}?)"/>.
    /// When fewer binary literals than text literals are available,
    /// the remaining text literals use their parsed values (mixed
    /// binary + text — useful when only some operands are decoded).
    /// </summary>
    public static EvaluationResult EvaluateWithBinaryLiterals(
        string expression,
        Func<int, double> slotLookup,
        IReadOnlyList<float>? binaryLiterals,
        Func<string, IReadOnlyList<double>, double?>? functionResolver = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var tokens = Tokenize(expression);
        var parser = new Parser(tokens);
        var ast = parser.ParseExpression();
        parser.ExpectEnd();
        var refs = new List<PowerFunctionRef>();
        var ctx = new EvalContext(slotLookup, functionResolver, binaryLiterals, refs);
        var value = ast.Evaluate(ctx);
        return new EvaluationResult(value, refs);
    }

    /// <summary>Mutable evaluation state — slot/function resolvers,
    /// binary-literal queue (Phase 3), and function-ref accumulator.
    /// Carried through the recursive <see cref="Node.Evaluate(EvalContext)"/>
    /// so each <see cref="NumberNode"/> can pull its value from the
    /// next binary literal when present.</summary>
    internal sealed class EvalContext
    {
        public Func<int, double> SlotLookup { get; }
        public Func<string, IReadOnlyList<double>, double?>? FunctionResolver { get; }
        public IReadOnlyList<float>? BinaryLiterals { get; }
        public List<PowerFunctionRef> Refs { get; }
        public int BinaryLiteralCursor;

        public EvalContext(
            Func<int, double> slotLookup,
            Func<string, IReadOnlyList<double>, double?>? functionResolver,
            IReadOnlyList<float>? binaryLiterals,
            List<PowerFunctionRef> refs)
        {
            SlotLookup = slotLookup;
            FunctionResolver = functionResolver;
            BinaryLiterals = binaryLiterals;
            Refs = refs;
            BinaryLiteralCursor = 0;
        }
    }

    /// <summary>Tokenize the expression. The grammar is small: numbers,
    /// SF_N references, identifiers (for function names), the four
    /// arithmetic operators, parens, braces, and the comma.</summary>
    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>(16);
        for (int i = 0; i < s.Length;)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Number literal (digits with optional decimal point).
            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                tokens.Add(new Token(TokenKind.Number, s.Substring(start, i - start)));
                continue;
            }

            // SF_N reference (bare or after a brace).
            if (c == 'S' && i + 3 < s.Length && s[i + 1] == 'F' && s[i + 2] == '_' && char.IsDigit(s[i + 3]))
            {
                int start = i;
                i += 3;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                tokens.Add(new Token(TokenKind.SlotRef, s.Substring(start, i - start)));
                continue;
            }

            // Identifier (function name).
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                tokens.Add(new Token(TokenKind.Identifier, s.Substring(start, i - start)));
                continue;
            }

            switch (c)
            {
                case '+': tokens.Add(new Token(TokenKind.Plus,  "+")); i++; break;
                case '-': tokens.Add(new Token(TokenKind.Minus, "-")); i++; break;
                case '*': tokens.Add(new Token(TokenKind.Star,  "*")); i++; break;
                case '/': tokens.Add(new Token(TokenKind.Slash, "/")); i++; break;
                case '(': tokens.Add(new Token(TokenKind.LParen, "(")); i++; break;
                case ')': tokens.Add(new Token(TokenKind.RParen, ")")); i++; break;
                case '{': tokens.Add(new Token(TokenKind.LBrace, "{")); i++; break;
                case '}': tokens.Add(new Token(TokenKind.RBrace, "}")); i++; break;
                case ',': tokens.Add(new Token(TokenKind.Comma,  ",")); i++; break;
                default:
                    throw new FormatException($"Unexpected character '{c}' at offset {i} in expression \"{s}\"");
            }
        }
        return tokens;
    }

    private enum TokenKind { Number, SlotRef, Identifier, Plus, Minus, Star, Slash, LParen, RParen, LBrace, RBrace, Comma }

    private readonly record struct Token(TokenKind Kind, string Text);

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public Parser(List<Token> tokens) { _tokens = tokens; _pos = 0; }

        public Node ParseExpression() => ParseAdditive();

        private Node ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (_pos < _tokens.Count &&
                   (_tokens[_pos].Kind == TokenKind.Plus || _tokens[_pos].Kind == TokenKind.Minus))
            {
                var op = _tokens[_pos++].Text;
                var right = ParseMultiplicative();
                left = new BinaryNode(op, left, right);
            }
            return left;
        }

        private Node ParseMultiplicative()
        {
            var left = ParseUnary();
            while (_pos < _tokens.Count &&
                   (_tokens[_pos].Kind == TokenKind.Star || _tokens[_pos].Kind == TokenKind.Slash))
            {
                var op = _tokens[_pos++].Text;
                var right = ParseUnary();
                left = new BinaryNode(op, left, right);
            }
            return left;
        }

        private Node ParseUnary()
        {
            if (_pos < _tokens.Count && _tokens[_pos].Kind == TokenKind.Minus)
            {
                _pos++;
                return new UnaryNode("-", ParsePrimary());
            }
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            if (_pos >= _tokens.Count) throw new FormatException("Unexpected end of expression");
            var t = _tokens[_pos];

            switch (t.Kind)
            {
                case TokenKind.Number:
                    _pos++;
                    return new NumberNode(double.Parse(t.Text, CultureInfo.InvariantCulture));

                case TokenKind.SlotRef:
                {
                    _pos++;
                    int idx = int.Parse(t.Text.AsSpan(3), CultureInfo.InvariantCulture);
                    return new SlotRefNode(idx);
                }

                case TokenKind.LBrace:
                {
                    // {SF_N} — braces are stylistic; consume and require an SlotRef inside.
                    _pos++;
                    if (_pos >= _tokens.Count || _tokens[_pos].Kind != TokenKind.SlotRef)
                        throw new FormatException("Expected SF_N inside braces");
                    var sref = _tokens[_pos++];
                    if (_pos >= _tokens.Count || _tokens[_pos].Kind != TokenKind.RBrace)
                        throw new FormatException("Expected '}' after SF_N");
                    _pos++;
                    int idx = int.Parse(sref.Text.AsSpan(3), CultureInfo.InvariantCulture);
                    return new SlotRefNode(idx);
                }

                case TokenKind.LParen:
                {
                    _pos++;
                    var inner = ParseExpression();
                    if (_pos >= _tokens.Count || _tokens[_pos].Kind != TokenKind.RParen)
                        throw new FormatException("Expected ')'");
                    _pos++;
                    return inner;
                }

                case TokenKind.Identifier:
                {
                    var name = t.Text;
                    _pos++;
                    if (_pos < _tokens.Count && _tokens[_pos].Kind == TokenKind.LParen)
                    {
                        _pos++;
                        var args = new List<Node>();
                        if (_pos < _tokens.Count && _tokens[_pos].Kind != TokenKind.RParen)
                        {
                            args.Add(ParseExpression());
                            while (_pos < _tokens.Count && _tokens[_pos].Kind == TokenKind.Comma)
                            {
                                _pos++;
                                args.Add(ParseExpression());
                            }
                        }
                        if (_pos >= _tokens.Count || _tokens[_pos].Kind != TokenKind.RParen)
                            throw new FormatException($"Expected ')' to close call to '{name}'");
                        _pos++;
                        return new FunctionNode(name, args);
                    }
                    // Bare identifier without parentheses — treat as zero-arg function call.
                    return new FunctionNode(name, Array.Empty<Node>());
                }

                default:
                    throw new FormatException($"Unexpected token '{t.Text}' at position {_pos}");
            }
        }

        public void ExpectEnd()
        {
            if (_pos != _tokens.Count)
                throw new FormatException(
                    $"Unexpected trailing token '{_tokens[_pos].Text}' at position {_pos}");
        }
    }

    private abstract record Node
    {
        public abstract double Evaluate(EvalContext ctx);
    }

    private sealed record NumberNode(double Value) : Node
    {
        /// <summary>Resolve the literal's value. When the evaluator was
        /// invoked through <see cref="EvaluateWithBinaryLiterals"/> with
        /// a non-empty binary-literal list, consume the NEXT binary
        /// literal in left-to-right encounter order — this is the
        /// engine-stored IEEE-754 single, canonical over the text-parsed
        /// value. Falls back to <see cref="Value"/> (text-parsed) when
        /// no binary literal is queued, so partial-binary contexts work
        /// seamlessly.</summary>
        public override double Evaluate(EvalContext ctx)
        {
            if (ctx.BinaryLiterals is { Count: > 0 } lits &&
                ctx.BinaryLiteralCursor < lits.Count)
            {
                return lits[ctx.BinaryLiteralCursor++];
            }
            return Value;
        }
    }

    private sealed record SlotRefNode(int Index) : Node
    {
        public override double Evaluate(EvalContext ctx) => ctx.SlotLookup(Index);
    }

    private sealed record UnaryNode(string Op, Node Operand) : Node
    {
        public override double Evaluate(EvalContext ctx)
        {
            var v = Operand.Evaluate(ctx);
            return Op switch
            {
                "-" => -v,
                _ => throw new InvalidOperationException($"Unknown unary op '{Op}'"),
            };
        }
    }

    private sealed record BinaryNode(string Op, Node Left, Node Right) : Node
    {
        public override double Evaluate(EvalContext ctx)
        {
            var l = Left.Evaluate(ctx);
            var r = Right.Evaluate(ctx);
            return Op switch
            {
                "+" => l + r,
                "-" => l - r,
                "*" => l * r,
                "/" => l / r,
                _ => throw new InvalidOperationException($"Unknown binary op '{Op}'"),
            };
        }
    }

    private sealed record FunctionNode(string Name, IReadOnlyList<Node> Args) : Node
    {
        public override double Evaluate(EvalContext ctx)
        {
            var argValues = new double[Args.Count];
            for (int i = 0; i < Args.Count; i++)
                argValues[i] = Args[i].Evaluate(ctx);

            // Surface the call to the caller regardless of whether a
            // resolver is registered (callers want to enumerate which
            // engine functions a power's formulas reference).
            ctx.Refs.Add(new PowerFunctionRef(Name, argValues));

            var resolved = ctx.FunctionResolver?.Invoke(Name, argValues);
            return resolved ?? double.NaN;
        }
    }
}

/// <summary>FR-C13 Phase 2 — one engine-function reference that appears
/// in a Power's <c>DT_STRING_FORMULA</c> text expressions or in its
/// localized <see cref="PowerDefinition.Description"/> format string.
/// The library surfaces the reference structurally (name + resolved arg
/// values); the consumer registers a resolver delegate keyed by
/// <see cref="Name"/> to substitute the engine value at runtime
/// (engine-runtime player-state is outside CASC's domain).</summary>
/// <param name="Name">The engine-function identifier as it appears in
/// the expression text (e.g. <c>"PlayerHealthMax"</c>). Case-sensitive
/// match with whatever identifier convention the engine uses.</param>
/// <param name="Args">The resolved argument values (each evaluated as
/// a <see cref="double"/>) the engine passes to the function call.
/// Empty for zero-argument calls (the common case for engine state
/// accessors).</param>
public readonly record struct PowerFunctionRef(
    string Name,
    IReadOnlyList<double> Args);
