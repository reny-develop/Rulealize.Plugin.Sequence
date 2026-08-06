// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction;
using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Nodes;
using Rulealize.Abstraction.Values;

namespace Rulealize.Plugin.Sequence
{
    /// <summary>The empty sequence.</summary>
    /// <remarks>
    /// Othello's way of saying "nothing is captured in this direction". Because
    /// <c>seq.selectMany</c> concatenates, the directions that capture nothing disappear
    /// from the result without anyone filtering them out.
    /// </remarks>
    internal sealed class EmptyNode : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context) => new EmptyNode();

        public override RuleValue Evaluate(IEvaluationContext context) => RuleValue.EmptySequence;
    }

    /// <summary>Whether any element satisfies <c>predicate</c>, or the sequence is non-empty.</summary>
    /// <remarks>
    /// <para>
    /// Short-circuits on the first element that qualifies, and that is worth more than it
    /// looks. Othello asks whether the player to move has any legal move at all in order to
    /// decide whether passing is allowed. The answer is usually yes and usually found
    /// early; without short-circuiting, every pass check would walk sixty-four squares and
    /// eight rays out of each of them.
    /// </para>
    /// <para>
    /// An empty sequence is always false.
    /// </para>
    /// </remarks>
    internal sealed class AnyNode(ExpressionNode source, LocalSlot? element, ExpressionNode? predicate)
        : IterationNode(source, element)
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            (ExpressionNode source, LocalSlot? element, ExpressionNode? predicate) =
                BuildParts(context, "predicate", required: false);

            return new AnyNode(source, element, predicate);
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            SequenceValue source = Source.Evaluate(context).AsSequence("seq.any.source");
            foreach (RuleValue item in source)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (predicate is null)
                {
                    return RuleValue.True;
                }

                if (predicate.Evaluate(BindElement(context, item)).AsBoolean("seq.any.predicate"))
                {
                    return RuleValue.True;
                }
            }

            return RuleValue.False;
        }
    }

    /// <summary>How many elements there are, or how many satisfy <c>where</c>.</summary>
    /// <remarks>
    /// Never short-circuits, since a count is not known until the walk finishes.
    /// </remarks>
    internal sealed class CountNode(ExpressionNode source, LocalSlot? element, ExpressionNode? filter)
        : IterationNode(source, element)
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            (ExpressionNode source, LocalSlot? element, ExpressionNode? filter) =
                BuildParts(context, "where", required: false);

            return new CountNode(source, element, filter);
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            SequenceValue source = Source.Evaluate(context).AsSequence("seq.count.source");
            int count = 0;
            foreach (RuleValue item in source)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (filter is null || filter.Evaluate(BindElement(context, item)).AsBoolean("seq.count.where"))
                {
                    count++;
                }
            }

            return RuleValue.Number(count);
        }
    }

    /// <summary>The element at <c>index</c>, counting from zero, or null when there is none.</summary>
    /// <remarks>
    /// <para>
    /// Reading past the end is <em>not</em> an error. It is where the value model's null
    /// chain starts, and Othello's flip rule is built on it: past the end gives null, a null
    /// coordinate gives null from <c>grid.at</c>, and comparing that against a colour gives
    /// false. The ray that runs off the board takes the else arm without the rule author
    /// writing a bounds check.
    /// </para>
    /// <para>
    /// A negative or fractional index is a different matter and does fault. That is not
    /// pointing outside the sequence; it is not an index at all.
    /// </para>
    /// <para>
    /// A sequence is not necessarily random-access, so this walks from the front.
    /// </para>
    /// </remarks>
    internal sealed class ElementAtNode(ExpressionNode source, ExpressionNode index) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context) =>
            new ElementAtNode(context.RequireExpression("source"), context.RequireExpression("index"));

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            SequenceValue elements = source.Evaluate(context).AsSequence("seq.elementAt.source");
            int wanted = index.Evaluate(context).AsInt32("seq.elementAt.index");
            if (wanted < 0)
            {
                throw new RuleEvaluationException("seq.elementAt.index", $"An index cannot be negative, but is {wanted}.");
            }

            int position = 0;
            foreach (RuleValue item in elements)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (position++ == wanted)
                {
                    return item;
                }
            }

            return RuleValue.Null;
        }
    }
}
