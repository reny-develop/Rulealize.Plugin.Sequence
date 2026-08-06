// Copyright (c) 2026 Reny
// Licensed under the Apache License, Version 2.0.

using Rulealize.Abstraction.Building;
using Rulealize.Abstraction.Evaluation;
using Rulealize.Abstraction.Nodes;
using Rulealize.Abstraction.Values;

namespace Rulealize.Plugin.Sequence
{
    /// <summary>The leading elements that satisfy <c>predicate</c>.</summary>
    /// <remarks>
    /// <para>
    /// Stops at the first element that fails, and does not include it. Nothing beyond that
    /// point is evaluated.
    /// </para>
    /// <para>
    /// This is the heart of how Othello's capture rule is expressed. A capture is a run of
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
    /// Nothing is de-duplicated. Othello's eight rays out of a square are disjoint, so
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
