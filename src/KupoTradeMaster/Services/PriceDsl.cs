using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KupoTradeMaster.Services;

/// TSM-inspired price expression language. Supports numeric literals, named sources, arithmetic
/// (+ - * / and unary -), grouping with parentheses, and functions max/min/avg/round/floor/ceil/if.
///
/// Named sources (all resolve to a double from the current item context):
///   mbmin, dbminbuyout  — min listing price on the current DC scope (all worlds)
///   mbminhw             — min listing price on your home world only (0 if none)
///   mbavg, dbmarket     — Universalis current average price (DC-wide, sales-weighted)
///   mbavghw             — average of active listings on your home world only
///   velocity, sellrate  — sales per day
///   vendor              — Lumina Item.PriceLow (NPC sale value)
///   taxrate             — current MB tax rate (0.03..0.05)
///
/// Examples:
///   dbmarket * 0.7                      — 70% of avg market
///   max(dbmarket * 0.7, vendor * 1.5)   — the higher of "70% market" or "1.5× vendor"
///   if(velocity - 1, dbmarket * 0.8, 0) — if selling >1/day, threshold at 80% market; else zero (don't buy)
public sealed class PriceDsl
{
    public sealed class ParseError : Exception { public ParseError(string msg) : base(msg) { } }

    public interface INode { double Eval(PriceDslContext ctx); }

    public static INode Compile(string source, IReadOnlyDictionary<string, INode>? customSources = null)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ParseError("empty formula");
        var tokens = Tokenize(source);
        var pos = 0;
        var node = ParseExpr(tokens, ref pos, customSources);
        if (tokens[pos].Kind != Tk.End) throw new ParseError($"unexpected trailing tokens at position {tokens[pos].Pos}");
        return node;
    }

    public static bool TryCompile(string source, out INode? node, out string? error)
        => TryCompile(source, null, out node, out error);

    public static bool TryCompile(string source, IReadOnlyDictionary<string, INode>? customSources,
                                    out INode? node, out string? error)
    {
        try { node = Compile(source, customSources); error = null; return true; }
        catch (ParseError ex) { node = null; error = ex.Message; return false; }
    }

    /// Compile a batch of named custom sources. Later sources may reference earlier ones.
    /// Cycles or forward references cause the offending source to fall back to a zero-node.
    public static Dictionary<string, INode> CompileCustomSources(IReadOnlyDictionary<string, string> sources)
    {
        var built = new Dictionary<string, INode>(sources.Count);
        foreach (var (name, formula) in sources)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(formula)) continue;
            if (TryCompile(formula, built, out var node, out _) && node != null)
                built[name.ToLowerInvariant()] = node;
        }
        return built;
    }

    // ---------- tokenizer ----------
    private enum Tk { Num, Ident, LP, RP, Comma, Plus, Minus, Star, Slash, End }
    private readonly record struct Token(Tk Kind, string Text, double Number, int Pos);

    private static List<Token> Tokenize(string s)
    {
        var t = new List<Token>();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                var start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                var lit = s[start..i];
                if (!double.TryParse(lit, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    throw new ParseError($"bad number '{lit}' at {start}");
                t.Add(new Token(Tk.Num, lit, n, start));
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                t.Add(new Token(Tk.Ident, s[start..i], 0, start));
                continue;
            }
            switch (c)
            {
                case '(': t.Add(new Token(Tk.LP, "(", 0, i)); i++; break;
                case ')': t.Add(new Token(Tk.RP, ")", 0, i)); i++; break;
                case ',': t.Add(new Token(Tk.Comma, ",", 0, i)); i++; break;
                case '+': t.Add(new Token(Tk.Plus, "+", 0, i)); i++; break;
                case '-': t.Add(new Token(Tk.Minus, "-", 0, i)); i++; break;
                case '*': t.Add(new Token(Tk.Star, "*", 0, i)); i++; break;
                case '/': t.Add(new Token(Tk.Slash, "/", 0, i)); i++; break;
                default: throw new ParseError($"unexpected char '{c}' at {i}");
            }
        }
        t.Add(new Token(Tk.End, "", 0, s.Length));
        return t;
    }

    // ---------- parser ----------
    private static INode ParseExpr(List<Token> t, ref int p, IReadOnlyDictionary<string, INode>? custom)
    {
        var left = ParseTerm(t, ref p, custom);
        while (t[p].Kind is Tk.Plus or Tk.Minus)
        {
            var op = t[p].Kind == Tk.Plus ? '+' : '-';
            p++;
            var right = ParseTerm(t, ref p, custom);
            left = new Binary { Op = op, L = left, R = right };
        }
        return left;
    }

    private static INode ParseTerm(List<Token> t, ref int p, IReadOnlyDictionary<string, INode>? custom)
    {
        var left = ParseFactor(t, ref p, custom);
        while (t[p].Kind is Tk.Star or Tk.Slash)
        {
            var op = t[p].Kind == Tk.Star ? '*' : '/';
            p++;
            var right = ParseFactor(t, ref p, custom);
            left = new Binary { Op = op, L = left, R = right };
        }
        return left;
    }

    private static INode ParseFactor(List<Token> t, ref int p, IReadOnlyDictionary<string, INode>? custom)
    {
        var tk = t[p];
        switch (tk.Kind)
        {
            case Tk.Minus:
                p++;
                return new Unary { Inner = ParseFactor(t, ref p, custom) };
            case Tk.Num:
                p++;
                return new Number { V = tk.Number };
            case Tk.LP:
                p++;
                var inside = ParseExpr(t, ref p, custom);
                if (t[p].Kind != Tk.RP) throw new ParseError($"missing ')' at {t[p].Pos}");
                p++;
                return inside;
            case Tk.Ident:
                var name = tk.Text.ToLowerInvariant();
                p++;
                if (t[p].Kind == Tk.LP)
                {
                    p++;
                    var args = new List<INode>();
                    if (t[p].Kind != Tk.RP)
                    {
                        args.Add(ParseExpr(t, ref p, custom));
                        while (t[p].Kind == Tk.Comma) { p++; args.Add(ParseExpr(t, ref p, custom)); }
                    }
                    if (t[p].Kind != Tk.RP) throw new ParseError($"missing ')' after args at {t[p].Pos}");
                    p++;
                    return new Call { Name = name, Args = args };
                }
                // If the identifier matches a pre-compiled custom source, inline that node.
                if (custom != null && custom.TryGetValue(name, out var resolved))
                    return resolved;
                return new Source { Name = name };
            default:
                throw new ParseError($"unexpected token '{tk.Text}' at {tk.Pos}");
        }
    }

    // ---------- AST ----------
    private sealed class Number : INode
    {
        public double V;
        public double Eval(PriceDslContext c) => V;
    }
    private sealed class Source : INode
    {
        public string Name = "";
        public double Eval(PriceDslContext c) => c.Get(Name);
    }
    private sealed class Unary : INode
    {
        public INode Inner = null!;
        public double Eval(PriceDslContext c) => -Inner.Eval(c);
    }
    private sealed class Binary : INode
    {
        public char Op;
        public INode L = null!, R = null!;
        public double Eval(PriceDslContext c) => Op switch
        {
            '+' => L.Eval(c) + R.Eval(c),
            '-' => L.Eval(c) - R.Eval(c),
            '*' => L.Eval(c) * R.Eval(c),
            '/' => SafeDiv(L.Eval(c), R.Eval(c)),
            _ => 0
        };
    }
    private sealed class Call : INode
    {
        public string Name = "";
        public List<INode> Args = new();
        public double Eval(PriceDslContext c)
        {
            var v = Args.Select(a => a.Eval(c)).ToArray();
            return Name switch
            {
                "max" => v.Length == 0 ? 0 : v.Max(),
                "min" => v.Length == 0 ? 0 : v.Min(),
                "avg" => v.Length == 0 ? 0 : v.Average(),
                "round" => v.Length == 1 ? Math.Round(v[0]) : 0,
                "floor" => v.Length == 1 ? Math.Floor(v[0]) : 0,
                "ceil"  => v.Length == 1 ? Math.Ceiling(v[0]) : 0,
                "if"    => v.Length == 3 ? (v[0] > 0 ? v[1] : v[2]) : 0,
                _ => 0
            };
        }
    }

    private static double SafeDiv(double a, double b) => b == 0 ? 0 : a / b;
}

public readonly record struct PriceDslContext(
    uint ItemId, double MinPrice, double AvgPrice, double Velocity, double Vendor, double TaxRate,
    double MinPriceHw = 0, double AvgPriceHw = 0)
{
    public double Get(string name) => name switch
    {
        "mbmin" or "dbminbuyout" => MinPrice,
        "mbminhw"                => MinPriceHw,
        "mbavg" or "dbmarket"    => AvgPrice,
        "mbavghw"                => AvgPriceHw,
        "velocity" or "sellrate" => Velocity,
        "vendor"                 => Vendor,
        "taxrate"                => TaxRate,
        _ => 0
    };
}
