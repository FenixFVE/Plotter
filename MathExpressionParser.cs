using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace MathExpressionParser;

// Token types for lexical analysis
public enum TokenType
{
    Number, Identifier, Plus, Minus, Multiply, Divide, Power,
    LeftParen, RightParen, Comma, Equals, EOF
}

public class Token
{
    public TokenType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public int Position { get; set; }
}

// Lexer for tokenizing the input
public class Lexer
{
    private readonly string _input;
    private int _position;

    public Lexer(string input)
    {
        _input = input;
        _position = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_position < _input.Length)
        {
            SkipWhitespace();
            if (_position >= _input.Length) break;

            var token = NextToken();
            if (token != null)
                tokens.Add(token);
        }

        tokens.Add(new Token { Type = TokenType.EOF, Position = _position });
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            _position++;
    }

    private Token NextToken()
    {
        var startPos = _position;
        var ch = _input[_position];

        if (char.IsDigit(ch) || ch == '.')
        {
            return ReadNumber();
        }
        else if (char.IsLetter(ch))
        {
            return ReadIdentifier();
        }
        else
        {
            _position++;
            return ch switch
            {
                '+' => new Token { Type = TokenType.Plus, Value = "+", Position = startPos },
                '-' => new Token { Type = TokenType.Minus, Value = "-", Position = startPos },
                '*' => new Token { Type = TokenType.Multiply, Value = "*", Position = startPos },
                '/' => new Token { Type = TokenType.Divide, Value = "/", Position = startPos },
                '^' => new Token { Type = TokenType.Power, Value = "^", Position = startPos },
                '(' => new Token { Type = TokenType.LeftParen, Value = "(", Position = startPos },
                ')' => new Token { Type = TokenType.RightParen, Value = ")", Position = startPos },
                ',' => new Token { Type = TokenType.Comma, Value = ",", Position = startPos },
                '=' => new Token { Type = TokenType.Equals, Value = "=", Position = startPos },
                _ => throw new Exception($"Unexpected character '{ch}' at position {startPos}")
            };
        }
    }

    private Token ReadNumber()
    {
        var startPos = _position;
        var number = "";
        var hasDecimalPoint = false;

        while (_position < _input.Length && (char.IsDigit(_input[_position]) || (_input[_position] == '.' && !hasDecimalPoint)))
        {
            if (_input[_position] == '.')
            {
                // Check if this is part of an identifier (like A.X) by looking ahead
                if (_position + 1 < _input.Length && char.IsLetter(_input[_position + 1]))
                {
                    break; // This dot belongs to an identifier, not a number
                }
                hasDecimalPoint = true;
            }
            number += _input[_position];
            _position++;
        }

        return new Token { Type = TokenType.Number, Value = number, Position = startPos };
    }

    private Token ReadIdentifier()
    {
        var startPos = _position;
        var identifier = "";

        while (_position < _input.Length &&
               (char.IsLetterOrDigit(_input[_position]) ||
                _input[_position] == '_' ||
                _input[_position] == '.'))
        {
            identifier += _input[_position];
            _position++;
        }

        return new Token { Type = TokenType.Identifier, Value = identifier, Position = startPos };
    }
}

// AST Node types
public abstract class AstNode { }

public class NumberNode : AstNode
{
    public double Value { get; set; }
}

public class IdentifierNode : AstNode
{
    public string Name { get; set; } = string.Empty;
}

public class BinaryOpNode : AstNode
{
    public AstNode Left { get; set; } = null!;
    public AstNode Right { get; set; } = null!;
    public TokenType Operator { get; set; }
}

public class UnaryOpNode : AstNode
{
    public AstNode Operand { get; set; } = null!;
    public TokenType Operator { get; set; }
}

public class FunctionCallNode : AstNode
{
    public string FunctionName { get; set; } = string.Empty;
    public List<AstNode> Arguments { get; set; } = new List<AstNode>();
}

public class FunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new List<string>();
    public AstNode Body { get; set; } = null!;
    public Delegate? CompiledFunction { get; set; }
    public bool IsConstant => Parameters.Count == 0;
    public double? ConstantValue { get; set; } // For modifiable constants
}

// Parser for building AST
public class Parser
{
    private List<Token> _tokens;
    private int _position;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
        _position = 0;
    }

    public FunctionDefinition ParseFunctionDefinition()
    {
        var functionName = Expect(TokenType.Identifier).Value;

        var parameters = new List<string>();

        // Check if this is a constant (no parentheses) or function
        if (Current().Type == TokenType.LeftParen)
        {
            Expect(TokenType.LeftParen);

            while (Current().Type != TokenType.RightParen)
            {
                parameters.Add(Expect(TokenType.Identifier).Value);
                if (Current().Type == TokenType.Comma)
                    Advance();
            }

            Expect(TokenType.RightParen);
        }

        Expect(TokenType.Equals);

        var body = ParseExpression();

        return new FunctionDefinition
        {
            Name = functionName,
            Parameters = parameters,
            Body = body
        };
    }

    public FunctionDefinition ParseAnonymousFunction()
    {
        var body = ParseExpression();

        // First validate that all identifiers are known constants, functions, or can be variables
        ValidateAllIdentifiers(body);

        var variables = ExtractVariables(body);

        // Sort variables for consistent parameter order
        variables.Sort();

        return new FunctionDefinition
        {
            Name = $"_anonymous_{Guid.NewGuid():N}",
            Parameters = variables,
            Body = body
        };
    }

    private void ValidateAllIdentifiers(AstNode node)
    {
        switch (node)
        {
            case IdentifierNode id:
                if (!IsBuiltInConstant(id.Name) &&
                    (_userConstantChecker == null || !_userConstantChecker(id.Name)))
                {
                    // It's not a constant, so it must be a variable (which is valid)
                }
                break;
            case BinaryOpNode binary:
                ValidateAllIdentifiers(binary.Left);
                ValidateAllIdentifiers(binary.Right);
                break;
            case UnaryOpNode unary:
                ValidateAllIdentifiers(unary.Operand);
                break;
            case FunctionCallNode funcCall:
                // Check if the function is a built-in math function or user-defined function
                if (!IsBuiltInFunction(funcCall.FunctionName) &&
                    (_userFunctionChecker == null || !_userFunctionChecker(funcCall.FunctionName)))
                {
                    throw new Exception($"Unknown function: {funcCall.FunctionName}");
                }
                foreach (var arg in funcCall.Arguments)
                {
                    ValidateAllIdentifiers(arg);
                }
                break;
        }
    }

    private void ValidateIdentifiers(AstNode node, List<string> allowedVariables)
    {
        switch (node)
        {
            case IdentifierNode id:
                if (!IsBuiltInConstant(id.Name) &&
                    (_userConstantChecker == null || !_userConstantChecker(id.Name)) &&
                    !allowedVariables.Contains(id.Name))
                {
                    throw new Exception($"Unknown identifier: {id.Name}");
                }
                break;
            case BinaryOpNode binary:
                ValidateIdentifiers(binary.Left, allowedVariables);
                ValidateIdentifiers(binary.Right, allowedVariables);
                break;
            case UnaryOpNode unary:
                ValidateIdentifiers(unary.Operand, allowedVariables);
                break;
            case FunctionCallNode funcCall:
                // Check if the function is a built-in math function or user-defined function
                if (!IsBuiltInFunction(funcCall.FunctionName) &&
                    (_userFunctionChecker == null || !_userFunctionChecker(funcCall.FunctionName)))
                {
                    throw new Exception($"Unknown function: {funcCall.FunctionName}");
                }
                foreach (var arg in funcCall.Arguments)
                {
                    ValidateIdentifiers(arg, allowedVariables);
                }
                break;
        }
    }

    private bool IsBuiltInFunction(string name)
    {
        return name == "sin" || name == "cos" || name == "tan" || name == "asin" || name == "acos" || name == "atan" ||
               name == "exp" || name == "log" || name == "log10" || name == "sqrt" || name == "abs" || name == "pow" ||
               name == "min" || name == "max" || name == "round" || name == "floor" || name == "ceiling" ||
               name == "sinh" || name == "cosh" || name == "tanh";
    }

    public void SetUserFunctionChecker(Func<string, bool> checker)
    {
        _userFunctionChecker = checker;
    }

    private Func<string, bool>? _userFunctionChecker;

    private List<string> ExtractVariables(AstNode node)
    {
        var variables = new HashSet<string>();
        ExtractVariablesRecursive(node, variables);
        return variables.ToList();
    }

    private void ExtractVariablesRecursive(AstNode node, HashSet<string> variables)
    {
        switch (node)
        {
            case IdentifierNode id:
                // Don't include built-in constants or user-defined constants
                if (!IsBuiltInConstant(id.Name) &&
                    (_userConstantChecker == null || !_userConstantChecker(id.Name)))
                {
                    variables.Add(id.Name);
                }
                break;
            case BinaryOpNode binary:
                ExtractVariablesRecursive(binary.Left, variables);
                ExtractVariablesRecursive(binary.Right, variables);
                break;
            case UnaryOpNode unary:
                ExtractVariablesRecursive(unary.Operand, variables);
                break;
            case FunctionCallNode funcCall:
                // Check if the function exists
                if (_userConstantChecker != null && funcCall.FunctionName != "sin" && funcCall.FunctionName != "cos" &&
                    funcCall.FunctionName != "tan" && funcCall.FunctionName != "exp" && funcCall.FunctionName != "log" &&
                    funcCall.FunctionName != "sqrt" && funcCall.FunctionName != "abs" && funcCall.FunctionName != "pow")
                {
                    // This could be a custom function - we should check if it exists
                    // For now, we'll assume it exists and let the expression builder handle the error
                }
                foreach (var arg in funcCall.Arguments)
                {
                    ExtractVariablesRecursive(arg, variables);
                }
                break;
        }
    }

    private bool IsBuiltInConstant(string name)
    {
        return name == "pi" || name == "Pi" || name == "PI" ||
               name == "e" || name == "E";
    }

    public void SetUserConstantChecker(Func<string, bool> checker)
    {
        _userConstantChecker = checker;
    }

    private Func<string, bool>? _userConstantChecker;

    public AstNode ParseExpression()
    {
        return ParseAdditive();
    }

    private AstNode ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (Current().Type == TokenType.Plus || Current().Type == TokenType.Minus)
        {
            var op = Current().Type;
            Advance();
            var right = ParseMultiplicative();
            left = new BinaryOpNode { Left = left, Right = right, Operator = op };
        }

        return left;
    }

    private AstNode ParseMultiplicative()
    {
        var left = ParsePower();

        while (Current().Type == TokenType.Multiply || Current().Type == TokenType.Divide)
        {
            var op = Current().Type;
            Advance();
            var right = ParsePower();
            left = new BinaryOpNode { Left = left, Right = right, Operator = op };
        }

        return left;
    }

    private AstNode ParsePower()
    {
        var left = ParseUnary();

        if (Current().Type == TokenType.Power)
        {
            Advance();
            var right = ParsePower(); // Right associative
            left = new BinaryOpNode { Left = left, Right = right, Operator = TokenType.Power };
        }

        return left;
    }

    private AstNode ParseUnary()
    {
        if (Current().Type == TokenType.Minus)
        {
            Advance();
            return new UnaryOpNode { Operand = ParseUnary(), Operator = TokenType.Minus };
        }

        return ParsePrimary();
    }

    private AstNode ParsePrimary()
    {
        var token = Current();

        if (token.Type == TokenType.Number)
        {
            Advance();
            return new NumberNode { Value = double.Parse(token.Value) };
        }
        else if (token.Type == TokenType.Identifier)
        {
            Advance();

            if (Current().Type == TokenType.LeftParen)
            {
                // Function call
                Advance();
                var args = new List<AstNode>();

                while (Current().Type != TokenType.RightParen)
                {
                    args.Add(ParseExpression());
                    if (Current().Type == TokenType.Comma)
                        Advance();
                }

                Expect(TokenType.RightParen);
                return new FunctionCallNode { FunctionName = token.Value, Arguments = args };
            }
            else
            {
                // Variable or constant
                return new IdentifierNode { Name = token.Value };
            }
        }
        else if (token.Type == TokenType.LeftParen)
        {
            Advance();
            var expr = ParseExpression();
            Expect(TokenType.RightParen);
            return expr;
        }

        throw new Exception($"Unexpected token: {token.Type}");
    }

    private Token Current()
    {
        return _tokens[_position];
    }

    private void Advance()
    {
        _position++;
    }

    private Token Expect(TokenType type)
    {
        var token = Current();
        if (token.Type != type)
            throw new Exception($"Expected {type} but got {token.Type}");
        Advance();
        return token;
    }
}

// Expression builder for converting AST to Expression trees
public class ExpressionBuilder
{
    private readonly Dictionary<string, Func<Expression[], Expression>> _mathFunctions;
    private readonly Dictionary<string, FunctionDefinition> _functions;
    private readonly Dictionary<string, double> _userConstants;

    public ExpressionBuilder()
    {
        _functions = new Dictionary<string, FunctionDefinition>();
        _userConstants = new Dictionary<string, double>();

        // Register built-in math functions with explicit parameter types to avoid ambiguity
        _mathFunctions = new Dictionary<string, Func<Expression[], Expression>>
        {
            ["sin"] = args => Expression.Call(typeof(Math).GetMethod("Sin", new[] { typeof(double) })!, args[0]),
            ["cos"] = args => Expression.Call(typeof(Math).GetMethod("Cos", new[] { typeof(double) })!, args[0]),
            ["tan"] = args => Expression.Call(typeof(Math).GetMethod("Tan", new[] { typeof(double) })!, args[0]),
            ["asin"] = args => Expression.Call(typeof(Math).GetMethod("Asin", new[] { typeof(double) })!, args[0]),
            ["acos"] = args => Expression.Call(typeof(Math).GetMethod("Acos", new[] { typeof(double) })!, args[0]),
            ["atan"] = args => Expression.Call(typeof(Math).GetMethod("Atan", new[] { typeof(double) })!, args[0]),
            ["exp"] = args => Expression.Call(typeof(Math).GetMethod("Exp", new[] { typeof(double) })!, args[0]),
            ["log"] = args => Expression.Call(typeof(Math).GetMethod("Log", new[] { typeof(double) })!, args[0]),
            ["log10"] = args => Expression.Call(typeof(Math).GetMethod("Log10", new[] { typeof(double) })!, args[0]),
            ["sqrt"] = args => Expression.Call(typeof(Math).GetMethod("Sqrt", new[] { typeof(double) })!, args[0]),
            ["abs"] = args => Expression.Call(typeof(Math).GetMethod("Abs", new[] { typeof(double) })!, args[0]),
            ["pow"] = args => Expression.Call(typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) })!, args[0], args[1]),
            ["min"] = args => Expression.Call(typeof(Math).GetMethod("Min", new[] { typeof(double), typeof(double) })!, args[0], args[1]),
            ["max"] = args => Expression.Call(typeof(Math).GetMethod("Max", new[] { typeof(double), typeof(double) })!, args[0], args[1]),
            ["round"] = args => Expression.Call(typeof(Math).GetMethod("Round", new[] { typeof(double) })!, args[0]),
            ["floor"] = args => Expression.Call(typeof(Math).GetMethod("Floor", new[] { typeof(double) })!, args[0]),
            ["ceiling"] = args => Expression.Call(typeof(Math).GetMethod("Ceiling", new[] { typeof(double) })!, args[0]),
            ["sinh"] = args => Expression.Call(typeof(Math).GetMethod("Sinh", new[] { typeof(double) })!, args[0]),
            ["cosh"] = args => Expression.Call(typeof(Math).GetMethod("Cosh", new[] { typeof(double) })!, args[0]),
            ["tanh"] = args => Expression.Call(typeof(Math).GetMethod("Tanh", new[] { typeof(double) })!, args[0])
        };
    }

    public void RegisterFunction(FunctionDefinition function)
    {
        _functions[function.Name] = function;

        // Compile the function immediately for better performance
        if (!function.IsConstant)
        {
            function.CompiledFunction = BuildDelegate(function);
        }
    }

    public bool HasFunction(string name)
    {
        return _functions.ContainsKey(name);
    }

    public FunctionDefinition? GetFunction(string name)
    {
        return _functions.TryGetValue(name, out var func) ? func : null;
    }

    public void SetConstant(string name, double value)
    {
        _userConstants[name] = value;

        // If there's a function definition for this constant, update it and recompile dependent functions
        if (_functions.ContainsKey(name) && _functions[name].IsConstant)
        {
            _functions[name].ConstantValue = value;
            // Invalidate compiled functions that might depend on this constant
            InvalidateDependentFunctions(name);
        }
    }

    public bool RemoveDefinition(string name)
    {
        bool removed = false;

        if (_functions.ContainsKey(name))
        {
            _functions.Remove(name);
            removed = true;
        }

        if (_userConstants.ContainsKey(name))
        {
            _userConstants.Remove(name);
            removed = true;
        }

        if (removed)
        {
            InvalidateDependentFunctions(name);
        }

        return removed;
    }

    public double? GetConstantValue(string name)
    {
        if (_userConstants.ContainsKey(name))
            return _userConstants[name];

        if (_functions.ContainsKey(name) && _functions[name].IsConstant && _functions[name].ConstantValue.HasValue)
            return _functions[name].ConstantValue!.Value;

        return null;
    }

    public List<string> GetAllDefinitions()
    {
        var result = new List<string>();
        result.AddRange(_functions.Keys);
        result.AddRange(_userConstants.Keys.Where(k => !_functions.ContainsKey(k)));
        return result;
    }

    public int? GetFunctionArgumentCount(string name)
    {
        if (_functions.ContainsKey(name))
        {
            return _functions[name].Parameters.Count;
        }
        return null;
    }

    private void InvalidateDependentFunctions(string constantName)
    {
        // Clear compiled functions that might depend on the changed constant
        foreach (var func in _functions.Values)
        {
            if (!func.IsConstant && func.CompiledFunction != null)
            {
                if (FunctionDependsOn(func.Body, constantName))
                {
                    func.CompiledFunction = null; // Will be recompiled on next use
                }
            }
        }
        {
            var parser = new MathParser();

            Console.WriteLine("\n=== Functions Without Arguments Test ===");

            // Test different syntaxes for functions without arguments
            try
            {
                // Syntax 1: f() = expression
                parser.AddDefinition("f() = 1 + sin(1)");
                var argCount1 = parser.GetFunctionArgumentCount("f");
                Console.WriteLine($"f() syntax: {argCount1} arguments");

                var f = parser.GetFunction<Func<double>>("f");
                Console.WriteLine($"f() = {f():F6}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"f() syntax failed: {ex.Message}");
            }

            try
            {
                // Syntax 2: g = expression (without parentheses)
                parser.AddDefinition("g = 2 + cos(0.5)");
                var argCount2 = parser.GetFunctionArgumentCount("g");
                Console.WriteLine($"g (no parentheses) syntax: {argCount2} arguments");

                var g = parser.GetFunction<Func<double>>("g");
                Console.WriteLine($"g() = {g():F6}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"g syntax failed: {ex.Message}");
            }

            try
            {
                // Syntax 3: More complex expression without arguments
                parser.AddDefinition("complexConstant() = sqrt(2) + log(e) + pi/4");
                var argCount3 = parser.GetFunctionArgumentCount("complexConstant");
                Console.WriteLine($"complexConstant() syntax: {argCount3} arguments");

                var complexConstant = parser.GetFunction<Func<double>>("complexConstant");
                Console.WriteLine($"complexConstant() = {complexConstant():F6}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"complexConstant() syntax failed: {ex.Message}");
            }

            try
            {
                // Syntax 4: Function that uses other constants
                parser.AddDefinition("combined() = f() + g + sqrt(complexConstant())");
                var argCount4 = parser.GetFunctionArgumentCount("combined");
                Console.WriteLine($"combined() syntax: {argCount4} arguments");

                var combined = parser.GetFunction<Func<double>>("combined");
                Console.WriteLine($"combined() = {combined():F6}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"combined() syntax failed: {ex.Message}");
            }

            Console.WriteLine("\n=== Comparison of Different Syntaxes ===");

            // Show all defined functions and their argument counts
            var allDefinitions = parser.GetAllDefinitions();
            foreach (var name in allDefinitions)
            {
                var count = parser.GetFunctionArgumentCount(name);
                if (count.HasValue)
                {
                    Console.WriteLine($"  {name}: {count.Value} argument(s)");

                    if (count.Value == 0)
                    {
                        try
                        {
                            var func = parser.GetFunction<Func<double>>(name);
                            Console.WriteLine($"    Value: {func():F6}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    Error evaluating: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    private bool FunctionDependsOn(AstNode node, string constantName)
    {
        switch (node)
        {
            case IdentifierNode id:
                return id.Name == constantName;
            case BinaryOpNode binary:
                return FunctionDependsOn(binary.Left, constantName) || FunctionDependsOn(binary.Right, constantName);
            case UnaryOpNode unary:
                return FunctionDependsOn(unary.Operand, constantName);
            case FunctionCallNode funcCall:
                return funcCall.Arguments.Any(arg => FunctionDependsOn(arg, constantName));
            default:
                return false;
        }
    }

    public Delegate BuildDelegate(FunctionDefinition function)
    {
        var parameters = function.Parameters.Select(p => Expression.Parameter(typeof(double), p)).ToList();
        var parameterDict = parameters.ToDictionary(p => p.Name!, p => p);

        var body = BuildExpression(function.Body, parameterDict);
        var lambda = Expression.Lambda(body, parameters);

        return lambda.Compile();
    }

    private Expression BuildExpression(AstNode node, Dictionary<string, ParameterExpression> parameters)
    {
        switch (node)
        {
            case NumberNode num:
                return Expression.Constant(num.Value);

            case IdentifierNode id:
                if (parameters.ContainsKey(id.Name))
                {
                    return parameters[id.Name];
                }
                else if (_userConstants.ContainsKey(id.Name))
                {
                    return Expression.Constant(_userConstants[id.Name]);
                }
                else if (_functions.ContainsKey(id.Name) && _functions[id.Name].IsConstant)
                {
                    // It's a constant (function with no parameters)
                    var constantFunc = _functions[id.Name];
                    if (constantFunc.ConstantValue.HasValue)
                    {
                        return Expression.Constant(constantFunc.ConstantValue.Value);
                    }
                    else
                    {
                        var constantBody = BuildExpression(constantFunc.Body, new Dictionary<string, ParameterExpression>());
                        return constantBody;
                    }
                }
                else if (id.Name == "pi" || id.Name == "Pi" || id.Name == "PI")
                    return Expression.Constant(Math.PI);
                else if (id.Name == "e" || id.Name == "E")
                    return Expression.Constant(Math.E);
                else
                    throw new Exception($"Unknown identifier: {id.Name}");

            case BinaryOpNode binary:
                var left = BuildExpression(binary.Left, parameters);
                var right = BuildExpression(binary.Right, parameters);

                return binary.Operator switch
                {
                    TokenType.Plus => Expression.Add(left, right),
                    TokenType.Minus => Expression.Subtract(left, right),
                    TokenType.Multiply => Expression.Multiply(left, right),
                    TokenType.Divide => Expression.Divide(left, right),
                    TokenType.Power => Expression.Call(typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) })!, left, right),
                    _ => throw new Exception($"Unknown binary operator: {binary.Operator}")
                };

            case UnaryOpNode unary:
                var operand = BuildExpression(unary.Operand, parameters);

                return unary.Operator switch
                {
                    TokenType.Minus => Expression.Negate(operand),
                    _ => throw new Exception($"Unknown unary operator: {unary.Operator}")
                };

            case FunctionCallNode funcCall:
                var args = funcCall.Arguments.Select(arg => BuildExpression(arg, parameters)).ToArray();

                if (_mathFunctions.ContainsKey(funcCall.FunctionName))
                {
                    return _mathFunctions[funcCall.FunctionName](args);
                }
                else if (_functions.ContainsKey(funcCall.FunctionName))
                {
                    // Inline user-defined function for maximum performance
                    var funcDef = _functions[funcCall.FunctionName];

                    if (funcDef.Parameters.Count != args.Length)
                        throw new Exception($"Function '{funcCall.FunctionName}' expects {funcDef.Parameters.Count} arguments but got {args.Length}");

                    // Create new parameter mapping for the inlined function
                    var newParams = new Dictionary<string, ParameterExpression>(parameters);
                    var substitutions = new Dictionary<string, Expression>();

                    for (int i = 0; i < funcDef.Parameters.Count; i++)
                    {
                        substitutions[funcDef.Parameters[i]] = args[i];
                    }

                    return SubstituteInExpression(funcDef.Body, substitutions, parameters);
                }
                else
                {
                    throw new Exception($"Unknown function: {funcCall.FunctionName}");
                }

            default:
                throw new Exception($"Unknown AST node type: {node.GetType().Name}");
        }
    }

    private Expression SubstituteInExpression(AstNode node, Dictionary<string, Expression> substitutions, Dictionary<string, ParameterExpression> originalParameters)
    {
        switch (node)
        {
            case NumberNode num:
                return Expression.Constant(num.Value);

            case IdentifierNode id:
                if (substitutions.ContainsKey(id.Name))
                {
                    return substitutions[id.Name];
                }
                else if (originalParameters.ContainsKey(id.Name))
                {
                    return originalParameters[id.Name];
                }
                else if (_userConstants.ContainsKey(id.Name))
                {
                    return Expression.Constant(_userConstants[id.Name]);
                }
                else if (_functions.ContainsKey(id.Name) && _functions[id.Name].IsConstant)
                {
                    var constantFunc = _functions[id.Name];
                    if (constantFunc.ConstantValue.HasValue)
                    {
                        return Expression.Constant(constantFunc.ConstantValue.Value);
                    }
                    else
                    {
                        return SubstituteInExpression(constantFunc.Body, substitutions, originalParameters);
                    }
                }
                else if (id.Name == "pi" || id.Name == "Pi" || id.Name == "PI")
                    return Expression.Constant(Math.PI);
                else if (id.Name == "e" || id.Name == "E")
                    return Expression.Constant(Math.E);
                else
                    throw new Exception($"Unknown identifier: {id.Name}");

            case BinaryOpNode binary:
                var left = SubstituteInExpression(binary.Left, substitutions, originalParameters);
                var right = SubstituteInExpression(binary.Right, substitutions, originalParameters);

                return binary.Operator switch
                {
                    TokenType.Plus => Expression.Add(left, right),
                    TokenType.Minus => Expression.Subtract(left, right),
                    TokenType.Multiply => Expression.Multiply(left, right),
                    TokenType.Divide => Expression.Divide(left, right),
                    TokenType.Power => Expression.Call(typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) })!, left, right),
                    _ => throw new Exception($"Unknown binary operator: {binary.Operator}")
                };

            case UnaryOpNode unary:
                var operand = SubstituteInExpression(unary.Operand, substitutions, originalParameters);

                return unary.Operator switch
                {
                    TokenType.Minus => Expression.Negate(operand),
                    _ => throw new Exception($"Unknown unary operator: {unary.Operator}")
                };

            case FunctionCallNode funcCall:
                var args = funcCall.Arguments.Select(arg => SubstituteInExpression(arg, substitutions, originalParameters)).ToArray();

                if (_mathFunctions.ContainsKey(funcCall.FunctionName))
                {
                    return _mathFunctions[funcCall.FunctionName](args);
                }
                else if (_functions.ContainsKey(funcCall.FunctionName))
                {
                    var funcDef = _functions[funcCall.FunctionName];

                    if (funcDef.Parameters.Count != args.Length)
                        throw new Exception($"Function '{funcCall.FunctionName}' expects {funcDef.Parameters.Count} arguments but got {args.Length}");

                    var newSubstitutions = new Dictionary<string, Expression>(substitutions);
                    for (int i = 0; i < funcDef.Parameters.Count; i++)
                    {
                        newSubstitutions[funcDef.Parameters[i]] = args[i];
                    }

                    return SubstituteInExpression(funcDef.Body, newSubstitutions, originalParameters);
                }
                else
                {
                    throw new Exception($"Unknown function: {funcCall.FunctionName}");
                }

            default:
                throw new Exception($"Unknown AST node type: {node.GetType().Name}");
        }
    }
}

// Main MathParser class with enhanced function composition support
public class MathParser
{
    private readonly ExpressionBuilder _expressionBuilder;
    private readonly List<string> _parseOrder;

    public MathParser()
    {
        _expressionBuilder = new ExpressionBuilder();
        _parseOrder = new List<string>();
    }

    public void AddDefinition(string equation)
    {
        var lexer = new Lexer(equation);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var function = parser.ParseFunctionDefinition();

        _expressionBuilder.RegisterFunction(function);
        if (!_parseOrder.Contains(function.Name))
            _parseOrder.Add(function.Name);
    }

    public Delegate GetFunction(string name)
    {
        var function = _expressionBuilder.GetFunction(name);
        if (function == null)
            throw new Exception($"Function '{name}' not found");

        if (function.CompiledFunction != null)
            return function.CompiledFunction;

        return _expressionBuilder.BuildDelegate(function);
    }

    public T GetFunction<T>(string name) where T : Delegate
    {
        return (T)GetFunction(name);
    }

    public Delegate Parse(string equation)
    {
        // Check if this is a named function definition or anonymous expression
        if (equation.Contains("="))
        {
            AddDefinition(equation);
            var lexer = new Lexer(equation);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var function = parser.ParseFunctionDefinition();

            return GetFunction(function.Name);
        }
        else
        {
            // Anonymous function
            var lexer = new Lexer(equation);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            parser.SetUserConstantChecker(name => _expressionBuilder.GetConstantValue(name).HasValue);
            parser.SetUserFunctionChecker(name => _expressionBuilder.HasFunction(name));
            var function = parser.ParseAnonymousFunction();

            return _expressionBuilder.BuildDelegate(function);
        }
    }

    public T Parse<T>(string equation) where T : Delegate
    {
        return (T)Parse(equation);
    }

    public void SetConstant(string name, double value)
    {
        _expressionBuilder.SetConstant(name, value);
    }

    public bool RemoveDefinition(string name)
    {
        _parseOrder.Remove(name);
        return _expressionBuilder.RemoveDefinition(name);
    }

    public double? GetConstantValue(string name)
    {
        return _expressionBuilder.GetConstantValue(name);
    }

    public List<string> GetAllDefinitions()
    {
        return _expressionBuilder.GetAllDefinitions();
    }

    public int? GetFunctionArgumentCount(string name)
    {
        return _expressionBuilder.GetFunctionArgumentCount(name);
    }

    public List<string> GetFunctionNames()
    {
        return new List<string>(_parseOrder);
    }

    public void Clear()
    {
        _parseOrder.Clear();
    }
}

// Example usage and comprehensive tests
class ProgramExample
{
    static void MainExample()
    {
        {
            var parser = new MathParser();

            Console.WriteLine("=== Новые возможности парсера ===\n");

            // 1. Тест неименованных функций
            Console.WriteLine("1. Неименованные функции:");

            var f1 = parser.Parse<Func<double, double>>("x + 2");
            Console.WriteLine($"   Parse(\"x + 2\"): f(3) = {f1(3)}"); // 5

            var f2 = parser.Parse<Func<double, double, double>>("x * y + 1");
            Console.WriteLine($"   Parse(\"x * y + 1\"): f(2, 3) = {f2(2, 3)}"); // 7

            var f3 = parser.Parse<Func<double, double>>("sin(x) + x^2");
            Console.WriteLine($"   Parse(\"sin(x) + x^2\"): f(1) = {f3(1):F6}"); // sin(1) + 1

            // 2. Тест констант в формате "текст.текст"
            Console.WriteLine("\n2. Константы в формате \"текст.текст\":");

            parser.SetConstant("A.X", 10.5);
            parser.SetConstant("Config.MaxValue", 100);
            parser.SetConstant("Math.MyPI", 3.14159);

            var f4 = parser.Parse<Func<double, double>>("x + A.X");
            Console.WriteLine($"   A.X = 10.5, Parse(\"x + A.X\"): f(5) = {f4(5)}"); // 15.5

            var f5 = parser.Parse<Func<double, double>>("Config.MaxValue - x");
            Console.WriteLine($"   Config.MaxValue = 100, Parse(\"Config.MaxValue - x\"): f(30) = {f5(30)}"); // 70

            // 3. Тест изменения констант
            Console.WriteLine("\n3. Изменение значений констант:");

            Console.WriteLine($"   A.X до изменения: {parser.GetConstantValue("A.X")}");
            parser.SetConstant("A.X", 20.0);
            Console.WriteLine($"   A.X после изменения: {parser.GetConstantValue("A.X")}");

            var f6 = parser.Parse<Func<double, double>>("x + A.X");
            Console.WriteLine($"   Parse(\"x + A.X\"): f(5) = {f6(5)}"); // 25

            // 4. Тест удаления определений
            Console.WriteLine("\n4. Удаление определений:");

            parser.AddDefinition("testFunc(x) = x * 2");
            parser.SetConstant("testConst", 42);

            Console.WriteLine($"   Определения до удаления: {string.Join(", ", parser.GetAllDefinitions())}");

            parser.RemoveDefinition("testFunc");
            parser.RemoveDefinition("testConst");

            Console.WriteLine($"   Определения после удаления: {string.Join(", ", parser.GetAllDefinitions())}");

            // 5. Комплексный тест с именованными и неименованными функциями
            Console.WriteLine("\n5. Комплексный тест:");

            parser.SetConstant("System.G", 9.81); // Ускорение свободного падения
            parser.AddDefinition("height(t) = System.G * t^2 / 2");

            var heightFunc = parser.GetFunction<Func<double, double>>("height");
            Console.WriteLine($"   height(2) = {heightFunc(2):F2} (именованная функция)");

            var velocityFunc = parser.Parse<Func<double, double>>("System.G * t");
            Console.WriteLine($"   velocity(2) = {velocityFunc(2):F2} (неименованная функция)");

            // 6. Проверка работы с существующими функциями
            Console.WriteLine("\n6. Совместимость с существующими функциями:");

            var originalFunc = parser.Parse<Func<double, double>>("trigFunc(x) = sin(x) + cos(x)");
            Console.WriteLine($"   trigFunc(π/4) = {originalFunc(Math.PI / 4):F6}");

            // 7. Тест композиции с константами
            Console.WriteLine("\n7. Композиция с константами:");

            parser.SetConstant("Scale.Factor", 2.5);
            parser.AddDefinition("scale(x) = x * Scale.Factor");

            var compositeFunc = parser.Parse<Func<double, double>>("compositeFunc(x) = scale(x) + A.X");
            Console.WriteLine($"   scale(x) + A.X где x=4: {compositeFunc(4):F1}"); // 4*2.5 + 20 = 30

            // 8. Тест многомерных функций с константами
            Console.WriteLine("\n8. Многомерные функции с константами:");

            parser.SetConstant("Physics.C", 299792458); // Скорость света
            var energyFunc = parser.Parse<Func<double, double>>("energyFunc(m) = m * Physics.C^2");
            Console.WriteLine($"   E = mc², m=1кг: E = {energyFunc(1):E2} Дж");

            // 9. Производительность неименованных функций
            Console.WriteLine("\n9. Тест производительности неименованных функций:");

            var perfFunc = parser.Parse<Func<double, double>>("sin(x) * cos(x) + x^2");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            double sum = 0;
            const int iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                sum += perfFunc(i * 0.001);
            }

            sw.Stop();
            Console.WriteLine($"   {iterations:N0} вызовов за {sw.ElapsedMilliseconds}мс");
            Console.WriteLine($"   Сумма: {sum:F6} (для предотвращения оптимизации)");

            // 10. Проверка обработки ошибок
            Console.WriteLine("\n10. Проверка обработки ошибок:");

            try
            {
                Console.WriteLine("   Попытка создать функцию с неизвестной функцией...");
                var errorFunc = parser.Parse<Func<double, double>>("unknownFunc(x) + 5");
                Console.WriteLine("   ОШИБКА: Должно было выбросить исключение для unknownFunc");
                Console.WriteLine($"   Но функция была создана: f(1) = {errorFunc(1)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✓ Корректно обработана ошибка: {ex.Message}");
            }

            // Тест с неизвестной переменной - это нормально для анонимных функций
            Console.WriteLine("   Создание функции с новой переменной (это нормально):");
            var newVarFunc = parser.Parse<Func<double, double>>("newVar + 5");
            Console.WriteLine($"   newVar + 5: f(3) = {newVarFunc(3)}"); // 3 + 5 = 8

            Console.WriteLine("\n=== Все новые функции успешно протестированы! ===");
        }
        {
            var parser = new MathParser();

            Console.WriteLine("=== Basic Function Examples ===");

            // Example 1: Simple function
            var f = parser.Parse<Func<double, double>>("f(x) = sin(x) + x^2");
            Console.WriteLine($"f(1) = {f(1):F6}"); // sin(1) + 1^2

            // Example 2: Multi-dimensional function
            var myFunc = parser.Parse<Func<double, double, double>>("myFunc(x,y) = x * (y + 2)");
            Console.WriteLine($"myFunc(3, 4) = {myFunc(3, 4):F6}"); // 3 * (4 + 2) = 18

            Console.WriteLine("\n=== Constants Support ===");

            // Example 3: Constants
            parser.AddDefinition("a = 5");
            var g = parser.Parse<Func<double, double, double>>("g(x,y) = a + sin(x) + x^2 + y");
            Console.WriteLine($"g(1, 2) = {g(1, 2):F6}"); // 5 + sin(1) + 1^2 + 2

            Console.WriteLine("\n=== Function Composition ===");

            // Example 4: Function composition
            parser.AddDefinition("h(x) = 2 + x");
            parser.AddDefinition("k(x) = x * h(x)");
            var k = parser.GetFunction<Func<double, double>>("k");
            Console.WriteLine($"k(3) = {k(3):F6}"); // 3 * (2 + 3) = 15

            // Example 5: Complex composition
            parser.AddDefinition("square(x) = x^2");
            parser.AddDefinition("cube(x) = x * square(x)");
            parser.AddDefinition("poly(x) = cube(x) + 2 * square(x) + x + 1");
            var poly = parser.GetFunction<Func<double, double>>("poly");
            Console.WriteLine($"poly(2) = {poly(2):F6}"); // 8 + 8 + 2 + 1 = 19

            Console.WriteLine("\n=== Advanced Math Functions ===");

            // Example 6: Complex expression with various math functions
            var complex = parser.Parse<Func<double, double>>("complex(x) = sqrt(abs(x)) + log(x + 1) * cos(x) + exp(-x^2/2)");
            Console.WriteLine($"complex(2) = {complex(2):F6}");

            Console.WriteLine("\n=== Performance Test ===");

            // Performance test
            var perfFunc = parser.Parse<Func<double, double>>("perf(x) = sin(x) * cos(x) + exp(-x^2) + sqrt(abs(x))");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            double sum = 0;
            const int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                sum += perfFunc(i * 0.001);
            }

            sw.Stop();
            Console.WriteLine($"{iterations:N0} evaluations took: {sw.ElapsedMilliseconds}ms ({(double)iterations / sw.ElapsedMilliseconds:F0}K evals/sec)");
            Console.WriteLine($"Sum: {sum:F6} (to prevent optimization)");

            Console.WriteLine("\n=== Multi-Variable Composition ===");

            // Example 7: Multi-variable function composition
            parser.AddDefinition("distance2D(x,y) = sqrt(x^2 + y^2)");
            parser.AddDefinition("circleArea(r) = pi * r^2");
            parser.AddDefinition("areaFromPoint(x,y) = circleArea(distance2D(x,y))");

            var areaFromPoint = parser.GetFunction<Func<double, double, double>>("areaFromPoint");
            Console.WriteLine($"areaFromPoint(3, 4) = {areaFromPoint(3, 4):F6}"); // π * 5^2 = 78.54

            Console.WriteLine("\n=== Trigonometric Identities ===");

            // Example 8: Trigonometric identity verification
            parser.AddDefinition("sinSquared(x) = sin(x)^2");
            parser.AddDefinition("cosSquared(x) = cos(x)^2");
            parser.AddDefinition("trigIdentity(x) = sinSquared(x) + cosSquared(x)");

            var trigIdentity = parser.GetFunction<Func<double, double>>("trigIdentity");
            Console.WriteLine($"sin(pi/4) + cos(pi/4) = {trigIdentity(Math.PI / 4):F6}"); // Should be 1.0

            Console.WriteLine("\nAll examples completed successfully!");
        }
        {
            var parser = new MathParser();
            parser.AddDefinition("a = 2");
            parser.AddDefinition("f(x) = x + a");
            var f = parser.GetFunction<Func<double, double>>("f");
            Console.WriteLine($"{f(0)} == 2");
            parser.SetConstant("a", 3);
            var f2 = parser.GetFunction<Func<double, double>>("f");
            Console.WriteLine($"{f2(0)} == 3");
        }
        {
            var parser = new MathParser();

            Console.WriteLine("\n=== Function Argument Count Test ===");

            // Define various functions
            parser.AddDefinition("constant = 42");
            parser.AddDefinition("singleArg(x) = x * 2");
            parser.AddDefinition("twoArgs(x,y) = x + y");
            parser.AddDefinition("threeArgs(x,y,z) = x * y + z");

            // Test argument count for each function
            Console.WriteLine($"constant: {parser.GetFunctionArgumentCount("constant")} arguments");
            Console.WriteLine($"singleArg: {parser.GetFunctionArgumentCount("singleArg")} arguments");
            Console.WriteLine($"twoArgs: {parser.GetFunctionArgumentCount("twoArgs")} arguments");
            Console.WriteLine($"threeArgs: {parser.GetFunctionArgumentCount("threeArgs")} arguments");

            // Test for non-existent function
            var nonExistent = parser.GetFunctionArgumentCount("nonExistent");
            Console.WriteLine($"nonExistent: {(nonExistent.HasValue ? nonExistent.Value.ToString() : "function not found")}");

            // Test with user constants
            parser.SetConstant("userConstant", 3.14);
            var userConstArg = parser.GetFunctionArgumentCount("userConstant");
            Console.WriteLine($"userConstant: {(userConstArg.HasValue ? userConstArg.Value.ToString() : "not a function")}");

            Console.WriteLine("\n=== Argument Count Validation Example ===");

            // Example of using argument count for validation
            string[] testFunctions = { "singleArg", "twoArgs", "threeArgs", "nonExistent" };

            foreach (var funcName in testFunctions)
            {
                var argCount = parser.GetFunctionArgumentCount(funcName);
                if (argCount.HasValue)
                {
                    Console.WriteLine($"Function '{funcName}' expects {argCount.Value} argument(s)");

                    // Example of how you might use this for validation
                    if (argCount.Value == 1)
                    {
                        var func = parser.GetFunction<Func<double, double>>(funcName);
                        Console.WriteLine($"  Calling {funcName}(5) = {func(5)}");
                    }
                    else if (argCount.Value == 2)
                    {
                        var func = parser.GetFunction<Func<double, double, double>>(funcName);
                        Console.WriteLine($"  Calling {funcName}(3, 4) = {func(3, 4)}");
                    }
                    else if (argCount.Value == 3)
                    {
                        var func = parser.GetFunction<Func<double, double, double, double>>(funcName);
                        Console.WriteLine($"  Calling {funcName}(1, 2, 3) = {func(1, 2, 3)}");
                    }
                    else if (argCount.Value == 0)
                    {
                        var func = parser.GetFunction<Func<double>>(funcName);
                        Console.WriteLine($"  Calling {funcName}() = {func()}");
                    }
                }
                else
                {
                    Console.WriteLine($"Function '{funcName}' not found");
                }
            }
        }
    }
}