// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Nodes;
using Rulealize.Abstraction.Values;

namespace Rulealize.Plugin.Sequence
{
    /// <summary>The shape the iterating operations share: a source, and a name for the element.</summary>
    /// <remarks>
    /// <para>
    /// The name given by <c>as</c> is visible only inside this node's predicate or
    /// projection, and shadows an outer binding of the same name. Reading it is somebody
    /// else's vocabulary — the binding plugin's <c>@</c> — and this plugin has no opinion on
    /// how that is spelled. It only declares the name; the scope machinery belongs to the
    /// runtime, which is what lets two plugins that never meet share a binding.
    /// </para>
    /// <para>
    /// <c>as</c> may be left out when the predicate or projection does not look at the
    /// element. Leaving it out and then writing a reference anyway is a build error raised
    /// by whoever owns the reference.
    /// </para>
    /// </remarks>
    internal abstract class IterationNode(ExpressionNode source, LocalSlot? element) : ExpressionNode
    {
        /// <summary>Gets the expression producing the sequence to walk.</summary>
        protected ExpressionNode Source => source;

        /// <summary>Binds the element, when this node named one.</summary>
        /// <param name="context">The context to extend.</param>
        /// <param name="value">The current element.</param>
        /// <returns>A context in which the body should be evaluated.</returns>
        protected IEvaluationContext BindElement(IEvaluationContext context, RuleValue value) =>
            element is LocalSlot slot ? context.Bind(slot, value) : context;

        /// <summary>Builds a source, an optional element name, and one body expression under it.</summary>
        /// <param name="context">The surrounding build state.</param>
        /// <param name="bodyProperty">The property holding the predicate or projection.</param>
        /// <param name="required">Whether that property must be present.</param>
        /// <returns>The three pieces, ready to hand to a constructor.</returns>
        protected static (ExpressionNode Source, LocalSlot? Element, ExpressionNode? Body) BuildParts(
            INodeBuildContext context,
            string bodyProperty,
            bool required)
        {
            // The source is built before the element name is declared, so a sequence
            // cannot be defined in terms of its own elements.
            ExpressionNode source = context.RequireExpression("source");

            string? name = context.OptionalString("as");
            if (name is null)
            {
                return (source, null, required ? context.RequireExpression(bodyProperty) : context.OptionalExpression(bodyProperty));
            }

            using (context.Scope.BeginScope())
            {
                LocalSlot element = context.Scope.Declare(name);
                ExpressionNode? body = required
                    ? context.RequireExpression(bodyProperty)
                    : context.OptionalExpression(bodyProperty);

                return (source, element, body);
            }
        }
    }
}
