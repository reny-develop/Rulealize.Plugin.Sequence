// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;
using Rulealize.Abstraction;
using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Node;
using Rulealize.Abstraction.Value;

namespace Rulealize.Plugin.Sequence
{
    /// <summary>The empty sequence.</summary>
    /// <remarks>
    /// Reversi's way of saying "nothing is captured in this direction". Because
    /// <c>seq.selectMany</c> concatenates, the directions that capture nothing disappear
    /// from the result without anyone filtering them out.
    /// </remarks>
    internal sealed class EmptyNode : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context) => new EmptyNode();

        public override RuleValue Evaluate(IEvaluationContext context) => RuleValue.EmptySequence;
    }

    /// <summary>A sequence written out element by element.</summary>
    /// <remarks>
    /// <para>
    /// The only way to write a sequence down. The value model gives sequences no JSON
    /// literal, so until this existed every sequence in a rule set had to originate in some
    /// other plugin — Reversi never noticed, because every sequence it uses comes out of a
    /// grid, but it left this plugin unable to produce anything at all except emptiness.
    /// </para>
    /// <para>
    /// Chess is where it shows. A knight's eight offsets are not a set any grid operation
    /// has a name for; they are simply eight directions, and a rule set needs to be able to
    /// say so.
    /// </para>
    /// <para>
    /// Elements are evaluated on each enumeration rather than once here, which keeps the
    /// re-enumerability requirement satisfied by construction and costs nothing, since every
    /// expression is pure.
    /// </para>
    /// </remarks>
    internal sealed class OfNode(ImmutableArray<ExpressionNode> elements) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new OfNode(context.RequireExpressionArray("of"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return elements.IsEmpty
                ? RuleValue.EmptySequence
                : RuleValue.Sequence(() => Enumerate(context));
        }

        private IEnumerable<RuleValue> Enumerate(IEvaluationContext context)
        {
            foreach (ExpressionNode element in elements)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                yield return element.Evaluate(context);
            }
        }
    }

    /// <summary>Whether any element satisfies <c>predicate</c>, or the sequence is non-empty.</summary>
    /// <remarks>
    /// <para>
    /// Short-circuits on the first element that qualifies, and that is worth more than it
    /// looks. Reversi asks whether the player to move has any legal move at all in order to
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

    /// <summary>The total of the elements, or of <c>select</c> applied to each of them.</summary>
    /// <remarks>
    /// <para>
    /// The second fold, and it waited for a rule set that wanted it. Blackjack is that rule
    /// set: a hand is a list of ranks and its total is what every rule about it asks for.
    /// Without this the total has to be carried in the state and maintained by each effect
    /// that draws a card, which puts two numbers in the schema that the cards already say.
    /// </para>
    /// <para>
    /// <c>select</c> is optional and mirrors what <c>where</c> is to <c>seq.count</c>. It
    /// earns its place for the same reason: what is being summed is rarely the element
    /// itself. A hand holds ranks and the sum wants their values, and a projection here
    /// saves wrapping the source in a <c>seq.select</c> that exists only to be consumed.
    /// </para>
    /// <para>
    /// The empty sequence totals zero, which is the identity rather than a special case,
    /// and no short-circuit is possible — a total is not known until the walk finishes.
    /// </para>
    /// </remarks>
    internal sealed class SumNode(ExpressionNode source, LocalSlot? element, ExpressionNode? projection)
        : IterationNode(source, element)
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            (ExpressionNode source, LocalSlot? element, ExpressionNode? projection) =
                BuildParts(context, "select", required: false);

            return new SumNode(source, element, projection);
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            SequenceValue source = Source.Evaluate(context).AsSequence("seq.sum.source");
            decimal total = 0;
            foreach (RuleValue item in source)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                total += projection is null
                    ? item.AsNumber("seq.sum.source")
                    : projection.Evaluate(BindElement(context, item)).AsNumber("seq.sum.select");
            }

            return RuleValue.Number(total);
        }
    }

    /// <summary>The element at <c>index</c>, counting from zero, or null when there is none.</summary>
    /// <remarks>
    /// <para>
    /// Reading past the end is <em>not</em> an error. It is where the value model's null
    /// chain starts, and Reversi's flip rule is built on it: past the end gives null, a null
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
