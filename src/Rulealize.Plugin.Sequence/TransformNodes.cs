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
    /// <summary>Several sequences run together into one.</summary>
    /// <remarks>
    /// <para>
    /// Expressible before this existed, as <c>seq.of</c> holding the parts and
    /// <c>seq.selectMany</c> projecting each to itself. That works, and it made the most
    /// ordinary thing anyone does to a sequence the hardest one to read — which is a reason
    /// to have the node, not a reason not to.
    /// </para>
    /// <para>
    /// Appending to a list field is what asks for it: a history grows by one position a move,
    /// and a rule set should be able to say so in a line.
    /// </para>
    /// </remarks>
    internal sealed class ConcatNode(ImmutableArray<ExpressionNode> parts) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new ConcatNode(context.RequireExpressionArray("of"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return RuleValue.Sequence(() => Walk(context));
        }

        private IEnumerable<RuleValue> Walk(IEvaluationContext context)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                foreach (RuleValue element in parts[i].Evaluate(context).AsSequence($"seq.concat.of[{i}]"))
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>The first <c>count</c> elements, or all of them if there are fewer.</summary>
    /// <remarks>
    /// A count past the end is not an error, the same way an index past the end of a sequence
    /// is not: how long a sequence is depends on the position, and a rule asking for ten of
    /// something that has six is asking a reasonable question.
    /// </remarks>
    internal sealed class TakeNode(ExpressionNode source, ExpressionNode count) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new TakeNode(context.RequireExpression("source"), context.RequireExpression("count"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            SequenceValue elements = source.Evaluate(context).AsSequence("seq.take.source");
            int wanted = Count.Read(count.Evaluate(context), "seq.take.count");
            return RuleValue.Sequence(() => elements.Take(wanted));
        }
    }

    /// <summary>Everything after the first <c>count</c> elements.</summary>
    /// <remarks>
    /// The other half of keeping a bounded history: a list capped at a hundred entries drops
    /// its oldest by skipping what is over.
    /// </remarks>
    internal sealed class SkipNode(ExpressionNode source, ExpressionNode count) : ExpressionNode
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new SkipNode(context.RequireExpression("source"), context.RequireExpression("count"));
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            SequenceValue elements = source.Evaluate(context).AsSequence("seq.skip.source");
            int dropped = Count.Read(count.Evaluate(context), "seq.skip.count");
            return RuleValue.Sequence(() => elements.Skip(dropped));
        }
    }

    /// <summary>Reads how many elements a node was asked for.</summary>
    internal static class Count
    {
        public static int Read(RuleValue value, string origin)
        {
            int count = value.AsInt32(origin);
            return count < 0
                ? throw new RuleEvaluationException(origin, $"A count cannot be negative, but is {count}.")
                : count;
        }
    }

    /// <summary>The leading elements that satisfy <c>predicate</c>.</summary>
    /// <remarks>
    /// <para>
    /// Stops at the first element that fails, and does not include it. Nothing beyond that
    /// point is evaluated.
    /// </para>
    /// <para>
    /// This is the heart of how Reversi's capture rule is expressed. A capture is a run of
    /// opposing stones with one of the mover's own immediately after it; this node finds the
    /// run, and <c>seq.elementAt</c> plus an equality test checks what closes it. The
    /// predicate is a single comparison against the opponent's colour, and it happens to
    /// end the walk correctly in all three cases that can end it — an empty square, one of
    /// the mover's own stones, and the edge of the board — because all three fail the same
    /// test.
    /// </para>
    /// </remarks>
    internal sealed class TakeWhileNode(ExpressionNode source, LocalSlot? element, ExpressionNode predicate)
        : IterationNode(source, element)
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            (ExpressionNode source, LocalSlot? element, ExpressionNode? predicate) =
                BuildParts(context, "predicate", required: true);

            return new TakeWhileNode(source, element, predicate!);
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            SequenceValue elements = Source.Evaluate(context).AsSequence("seq.takeWhile.source");
            return RuleValue.Sequence(() => Walk(context, elements));
        }

        private IEnumerable<RuleValue> Walk(IEvaluationContext context, SequenceValue elements)
        {
            foreach (RuleValue item in elements)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (!predicate.Evaluate(BindElement(context, item)).AsBoolean("seq.takeWhile.predicate"))
                {
                    yield break;
                }

                yield return item;
            }
        }
    }

    /// <summary>The elements that satisfy <c>predicate</c>, in order.</summary>
    internal sealed class WhereNode(ExpressionNode source, LocalSlot? element, ExpressionNode predicate)
        : IterationNode(source, element)
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            (ExpressionNode source, LocalSlot? element, ExpressionNode? predicate) =
                BuildParts(context, "predicate", required: true);

            return new WhereNode(source, element, predicate!);
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            SequenceValue elements = Source.Evaluate(context).AsSequence("seq.where.source");
            return RuleValue.Sequence(() => Walk(context, elements));
        }

        private IEnumerable<RuleValue> Walk(IEvaluationContext context, SequenceValue elements)
        {
            foreach (RuleValue item in elements)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (predicate.Evaluate(BindElement(context, item)).AsBoolean("seq.where.predicate"))
                {
                    yield return item;
                }
            }
        }
    }

    /// <summary>Each element replaced by <c>select</c>, in order.</summary>
    internal sealed class SelectNode(ExpressionNode source, LocalSlot? element, ExpressionNode projection)
        : IterationNode(source, element)
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            (ExpressionNode source, LocalSlot? element, ExpressionNode? projection) =
                BuildParts(context, "select", required: true);

            return new SelectNode(source, element, projection!);
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            SequenceValue elements = Source.Evaluate(context).AsSequence("seq.select.source");
            return RuleValue.Sequence(() => Walk(context, elements));
        }

        private IEnumerable<RuleValue> Walk(IEvaluationContext context, SequenceValue elements)
        {
            foreach (RuleValue item in elements)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                yield return projection.Evaluate(BindElement(context, item));
            }
        }
    }

    /// <summary>The sequences <c>select</c> produces for each element, concatenated in order.</summary>
    /// <remarks>
    /// <para>
    /// A projection that yields something other than a sequence is an evaluation error; a
    /// single value is not silently wrapped.
    /// </para>
    /// <para>
    /// Nothing is de-duplicated. Reversi's eight rays out of a square are disjoint, so
    /// concatenating what each of them captures cannot repeat a coordinate — but that is a
    /// property of the game's geometry, not a guarantee made here.
    /// </para>
    /// </remarks>
    internal sealed class SelectManyNode(ExpressionNode source, LocalSlot? element, ExpressionNode projection)
        : IterationNode(source, element)
    {
        public static ExpressionNode Build(INodeBuildContext context)
        {
            (ExpressionNode source, LocalSlot? element, ExpressionNode? projection) =
                BuildParts(context, "select", required: true);

            return new SelectManyNode(source, element, projection!);
        }

        public override RuleValue Evaluate(IEvaluationContext context)
        {
            SequenceValue elements = Source.Evaluate(context).AsSequence("seq.selectMany.source");
            return RuleValue.Sequence(() => Walk(context, elements));
        }

        private IEnumerable<RuleValue> Walk(IEvaluationContext context, SequenceValue elements)
        {
            foreach (RuleValue item in elements)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                SequenceValue inner = projection
                    .Evaluate(BindElement(context, item))
                    .AsSequence("seq.selectMany.select");

                foreach (RuleValue nested in inner)
                {
                    yield return nested;
                }
            }
        }
    }
}
